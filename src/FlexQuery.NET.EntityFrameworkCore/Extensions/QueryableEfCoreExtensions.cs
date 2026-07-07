using FlexQuery.NET.Builders;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.EntityFrameworkCore.SqlFormatting;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.EntityFrameworkCore.Includes;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.EntityFrameworkCore;

public static class QueryableEfCoreExtensions
{
    private static readonly MethodInfo ExecuteGroupedQueryMethod = typeof(QueryableEfCoreExtensions)
        .GetMethod(nameof(ExecuteGroupedQueryAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CountGroupedQueryMethod = typeof(QueryableEfCoreExtensions)
        .GetMethod(nameof(CountGroupedQueryAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static IQueryable<T> ApplyExpandIncludes<T>(
        this IQueryable<T> query,
        QueryOptions options)
        where T : class
    {
        if (options?.Expand == null || options.Expand.Count == 0) return query;
        return IncludeBuilder.Apply(query, options);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        Action<EfCoreQueryOptions>? configureQueryOptions = null,
        CancellationToken cancellationToken = default,
        Action<FlexQueryExecutionConfig>? configureExecution = null)
        where T : class
    {
        var efCoreQueryOptions = new EfCoreQueryOptions();
        configureQueryOptions?.Invoke(efCoreQueryOptions);

        return await query.FlexQueryAsync(parameters, efCoreQueryOptions, cancellationToken, configureExecution);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        EfCoreQueryOptions efCoreQueryOptions,
        CancellationToken cancellationToken = default,
        Action<FlexQueryExecutionConfig>? configureExecution = null)
        where T : class
    {
        var options = parameters.ToQueryOptions();

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        options = options.Normalize();
        options.ValidateOrThrow<T>(efCoreQueryOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, cancellationToken);
            if (ctx.Listener != null)
            {
                await ctx.Listener.QueryParsedAsync(
                    new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }
        }

        var hasProjection = options.HasProjection();

        return await query.ApplyFlexQueryAsync(options, hasProjection, efCoreQueryOptions, ctx);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        Action<EfCoreQueryOptions>? configureQueryOptions = null,
        CancellationToken cancellationToken = default,
        Action<FlexQueryExecutionConfig>? configureExecution = null)
        where T : class
    {
        var efCoreQueryOptions = new EfCoreQueryOptions();
        configureQueryOptions?.Invoke(efCoreQueryOptions);

        return await query.FlexQueryAsync(options, efCoreQueryOptions, cancellationToken, configureExecution);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        EfCoreQueryOptions efCoreQueryOptions,
        CancellationToken cancellationToken = default,
        Action<FlexQueryExecutionConfig>? configureExecution = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(efCoreQueryOptions);

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        options = options.Normalize();
        options.ValidateOrThrow<T>(efCoreQueryOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, cancellationToken);
            if (ctx.Listener != null)
            {
                await ctx.Listener.QueryParsedAsync(
                    new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }
        }

        var hasProjection = options.HasProjection();

        return await query.ApplyFlexQueryAsync(options, hasProjection, efCoreQueryOptions, ctx);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        QueryExecutionOptions executionOptions,
        CancellationToken cancellationToken = default,
        Action<FlexQueryExecutionConfig>? configureExecution = null)
        where T : class
    {
        return await query.FlexQueryAsync(parameters, new EfCoreQueryOptions
        {
            IncludeTotalCount = executionOptions.IncludeTotalCount,
            DefaultPageSize = executionOptions.DefaultPageSize,
            MaxPageSize = executionOptions.MaxPageSize,
            CaseInsensitive = executionOptions.CaseInsensitive,
            StrictFieldValidation = executionOptions.StrictFieldValidation,
            MaxFieldDepth = executionOptions.MaxFieldDepth,
            AllowedFields = executionOptions.AllowedFields,
            BlockedFields = executionOptions.BlockedFields,
            AllowedIncludes = executionOptions.AllowedIncludes,
            ExpressionMappings = executionOptions.ExpressionMappings,
            AllowedOperators = executionOptions.AllowedOperators,
            FilterableFields = executionOptions.FilterableFields,
            SortableFields = executionOptions.SortableFields,
            SelectableFields = executionOptions.SelectableFields,
            GroupableFields = executionOptions.GroupableFields,
            AggregatableFields = executionOptions.AggregatableFields,
            DefaultSortField = executionOptions.DefaultSortField,
            DefaultSortDescending = executionOptions.DefaultSortDescending,
            FieldMappings = executionOptions.FieldMappings,
            RoleAllowedFields = executionOptions.RoleAllowedFields,
            CurrentRole = executionOptions.CurrentRole,
            AllowedFieldsResolver = executionOptions.AllowedFieldsResolver
        }, cancellationToken, configureExecution);
    }

    private static async Task<QueryResult<object>> ApplyFlexQueryAsync<T>(this IQueryable<T> query, 
        QueryOptions options, bool hasProjection, EfCoreQueryOptions? efCoreQueryOptions = null, FlexQueryExecutionContext? ctx = null)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        if (efCoreQueryOptions?.UseNoTracking == true)
        {
            query = query.AsNoTracking();
        }

        var filtered = QueryBuilder.ApplyFilter(query, options);
        if (options.Distinct == true)
            filtered = filtered.Distinct();

        var total = efCoreQueryOptions?.IncludeTotalCount == true ? await filtered.CountAsync(ct) : (int?)null;

        if (options.GroupBy is { Count: > 0 })
        {
            var groupedQuery = GroupByBuilder.ApplyUntyped(filtered, options);

            if (ctx?.Listener is not null)
            {
                var (generatedQuery, queryParameters) = TryGetSqlWithParameters(groupedQuery);
                await ctx.Listener.QueryTranslatedAsync(
                    new QueryTranslatedEvent(ctx.QueryId, generatedQuery, queryParameters, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }

            var resultCount = efCoreQueryOptions?.IncludeTotalCount == true
                ? await CountGroupedQuery(groupedQuery, ct)
                : (int?)null;
            var data = await ExecuteGroupedQuery(groupedQuery, options, ct);
            var groupResult = options.BuildQueryResult(data, total, resultCount: resultCount);

            if (ctx?.Listener is not null)
            {
                var now = DateTimeOffset.UtcNow;
                await ctx.Listener.QueryExecutedAsync(
                    new QueryExecutedEvent(ctx.QueryId, data.Count, null, ctx.Stopwatch.Elapsed, now),
                    ctx.CancellationToken);
                await ctx.Listener.QueryMaterializedAsync(
                    new QueryMaterializedEvent(ctx.QueryId, groupResult, null, ctx.Stopwatch.Elapsed, now),
                    ctx.CancellationToken);
            }

            return groupResult;
        }

        filtered = QueryBuilder.ApplySort(filtered, options);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;

        if (options.Aggregates.Count > 0 &&
            (options.GroupBy == null || options.GroupBy.Count == 0))
        {
            var aggregateQuery = GroupByBuilder.Apply(filtered, options);

            var aggRow = await aggregateQuery.FirstOrDefaultAsync(ct);

            grandTotals = AggregateResultBuilder.Build(
                aggRow,
                options.Aggregates);
        }

        filtered = QueryBuilder.ApplyPaging(filtered, options);

        filtered = filtered.ApplyExpandIncludes(options);

        if (ctx?.Listener is not null)
        {
            var (sql, sparams) = TryGetSqlWithParameters(filtered);
            await ctx.Listener.QueryTranslatedAsync(
                new QueryTranslatedEvent(ctx.QueryId, sql, sparams, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        IReadOnlyList<object>? dataList = null;
        QueryResult<object>? result = null;

        try
        {
            if (hasProjection)
            {
                var projectedData = await filtered.ApplySelect(options).ToListAsync(ct);
                dataList = projectedData;

                if (ctx?.Listener is not null)
                {
                    await ctx.Listener.QueryExecutedAsync(
                        new QueryExecutedEvent(ctx.QueryId, dataList.Count, null, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                        ctx.CancellationToken);
                }

                result = options.BuildQueryResult(projectedData, total, grandTotals);
            }
            else
            {
                var filteredData = await filtered.ToListAsync(ct);
                dataList = filteredData;

                if (ctx?.Listener is not null)
                {
                    await ctx.Listener.QueryExecutedAsync(
                        new QueryExecutedEvent(ctx.QueryId, dataList.Count, null, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                        ctx.CancellationToken);
                }

                result = options.BuildQueryResult(filteredData, total, grandTotals).ToObjectResult();
            }

            if (ctx?.Listener is not null)
            {
                await ctx.Listener.QueryMaterializedAsync(
                    new QueryMaterializedEvent(ctx.QueryId, result, null, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            var now = DateTimeOffset.UtcNow;
            if (ctx?.Listener is not null)
            {
                if (dataList is not null)
                {
                    await ctx.Listener.QueryMaterializedAsync(
                        new QueryMaterializedEvent(ctx.QueryId, null, ex, ctx.Stopwatch.Elapsed, now),
                        ctx.CancellationToken);
                }
                else
                {
                    await ctx.Listener.QueryExecutedAsync(
                        new QueryExecutedEvent(ctx.QueryId, null, ex, ctx.Stopwatch.Elapsed, now),
                        ctx.CancellationToken);
                }
            }
            throw;
        }

        return result!;
    }

    private static (string? Sql, IReadOnlyList<QueryParameter>? Parameters) TryGetSqlWithParameters(IQueryable query)
    {
        try
        {
            var rawSql = query.ToQueryString();
            var (cleanSql, parameters) = SqlParameterExtractor.Extract(rawSql);
            try { return (SqlFormatter.Format(cleanSql), parameters); }
            catch { return (cleanSql, parameters); }
        }
        catch { return (null, null); }
    }

    private static Task<IReadOnlyList<object>> ExecuteGroupedQuery(
        IQueryable groupedQuery,
        QueryOptions options,
        CancellationToken cancellationToken)
    {
        return (Task<IReadOnlyList<object>>)ExecuteGroupedQueryMethod
            .MakeGenericMethod(groupedQuery.ElementType)
            .Invoke(null, [groupedQuery, options, cancellationToken])!;
    }

    private static Task<int> CountGroupedQuery(
        IQueryable groupedQuery,
        CancellationToken cancellationToken)
    {
        return (Task<int>)CountGroupedQueryMethod
            .MakeGenericMethod(groupedQuery.ElementType)
            .Invoke(null, [groupedQuery, cancellationToken])!;
    }

    private static Task<int> CountGroupedQueryAsync<TShape>(
        IQueryable groupedQuery,
        CancellationToken cancellationToken)
    {
        return ((IQueryable<TShape>)groupedQuery).CountAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<object>> ExecuteGroupedQueryAsync<TShape>(
        IQueryable groupedQuery,
        QueryOptions options,
        CancellationToken cancellationToken)
    {
        var typedQuery = (IQueryable<TShape>)groupedQuery;
        var sorts = BuildGroupedSorts(options);

        typedQuery = QueryBuilder.ApplySort(typedQuery, sorts, options);
        typedQuery = QueryBuilder.ApplyPaging(typedQuery, options);

        var rows = await typedQuery.ToListAsync(cancellationToken);
        return rows.Cast<object>().ToList();
    }

    private static List<SortNode> BuildGroupedSorts(QueryOptions options)
    {
        return GroupedSortValidator.Validate(
            options.Sort,
            options.GroupBy ?? [],
            options.Aggregates);
    }
}
