using FlexQuery.NET.Builders;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Execution;

/// <summary>
/// Orchestrates a single FlexQueryAsync EF Core execution end-to-end: normalizes
/// and validates the parsed <see cref="QueryOptions"/>, reports the parsed
/// event, then filters, sorts, aggregates, pages, includes, and materializes
/// the query — delegating to <see cref="GroupedQueryExecutor"/> when the
/// request groups results.
/// </summary>
internal static class FlexQueryEfCoreExecutor
{
    /// <summary>
    /// Entry point shared by all four public <c>FlexQueryAsync</c> overloads:
    /// normalize, validate, notify "parsed", then execute.
    /// </summary>
    public static async Task<QueryResult<object>> RunAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken)
        where T : class
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();
        
        queryOptions = queryOptions.Normalize();

        if (options.DisablePaging)
            queryOptions.Paging.Disabled = true;

        queryOptions.ValidateOrThrow<T>(options);

        var listener = options.Listener;

        var ctx = listener is not null
            ? new FlexQueryExecutionContext(listener, cancellationToken)
            : null;

        await ctx.NotifyParsedAsync(queryOptions);
        
        return await ExecuteAsync(query, queryOptions, options, ctx);
    }

    private static async Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions? options,
        FlexQueryExecutionContext? ctx)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;
        if (options?.UseNoTracking == true)
            query = query.AsNoTracking();

        var filtered = QueryBuilder.ApplyFilter(query, queryOptions);
        if (queryOptions.Distinct == true)
            filtered = filtered.Distinct();

        var total = options?.IncludeTotalCount == true ? await filtered.CountAsync(ct) : (int?)null;

        if (queryOptions.GroupBy is { Count: > 0 })
            return await GroupedQueryExecutor.ExecuteAsync(filtered, queryOptions, total, options, ctx, ct);

        filtered = QueryBuilder.ApplySort(filtered, queryOptions);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;

        if (queryOptions.Aggregates.Count > 0 && (queryOptions.GroupBy == null || queryOptions.GroupBy.Count == 0))
        {
            var aggregateQuery = GroupByBuilder.Apply(filtered, queryOptions);
            var aggRow = await aggregateQuery.FirstOrDefaultAsync(ct);
            grandTotals = AggregateResultBuilder.Build(aggRow, queryOptions.Aggregates);
        }

        filtered = queryOptions.IsKeysetMode 
            ? QueryBuilder.ApplyKeysetPaging(filtered, queryOptions)
            : QueryBuilder.ApplyPaging(filtered, queryOptions);

        // Note: ApplySelect already incorporates FilteredIncludes filters into the projection tree,
        // so calling ApplyExpand here is technically redundant but ensures consistency
        // if the projection engine behaviour changes.
        filtered = filtered.ApplyExpand(queryOptions);
        
        var (sql, queryParameters) = SqlQueryInspector.TryGetSqlWithParameters(filtered);

        if (ctx is not null) await ctx.NotifyTranslatedAsync(sql, queryParameters);

        return await MaterializeAsync(filtered, queryOptions, total, grandTotals, ctx);
    }

    private static async Task<QueryResult<object>> MaterializeAsync<T>(
        IQueryable<T> filtered,
        QueryOptions queryOptions,
        int? total,
        Dictionary<string, Dictionary<string, object>>? grandTotals,
        FlexQueryExecutionContext? ctx)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;
        IReadOnlyList<object>? dataList = null;

        try
        {
            QueryResult<object> result;

            if (queryOptions.HasProjection())
            {
                var projectedData = await filtered.ApplySelect(queryOptions).ToListAsync(ct);
                dataList = projectedData;
                if (ctx is not null) await ctx.NotifyExecutedAsync(dataList.Count);
                result = queryOptions.BuildQueryResult(projectedData, total, grandTotals);
            }
            else
            {
                var filteredData = await filtered.ToListAsync(ct);
                dataList = filteredData;
                if (ctx is not null) await ctx.NotifyExecutedAsync(dataList.Count);
                result = queryOptions.BuildQueryResult(filteredData, total, grandTotals).ToObjectResult();
            }

            if (ctx is not null) await ctx.NotifyMaterializedAsync(result);
            return result;
        }
        catch (Exception ex)
        {
            if (ctx is not null)
            {
                if (dataList is not null)
                    await ctx.NotifyMaterializedAsync(null, ex);
                else
                    await ctx.NotifyExecutedAsync(null, ex);
            }

            throw;
        }
    }
}