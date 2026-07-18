using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Materialization;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Sql.Utilities;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Execution;

/// <summary>
/// Orchestrates a single FlexQueryAsync Dapper execution end-to-end: stamps the
/// entity type, normalizes and validates options, reports the parsed event,
/// translates to SQL, materializes, computes counts/aggregates, and reports
/// the remaining diagnostic events.
/// </summary>
internal static class DapperQueryExecutor
{
    /// <summary>
    /// Entry point shared by all three public <c>FlexQueryAsync</c> overloads.
    /// </summary>
    public static async Task<QueryResult<object>> RunAsync<T>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        queryOptions.Items[ContextKeys.EntityType] = typeof(T);

        queryOptions = queryOptions.Normalize();

        if (options.DisablePaging)
            queryOptions.Paging.Disabled = true;

        queryOptions.ValidateOrThrow(typeof(T), options);

        var listener = options.Listener;
        
        var ctx = listener is not null
            ? new FlexQueryExecutionContext(listener, cancellationToken)
            : null;

        await ctx.NotifyParsedAsync(queryOptions);

        return await ExecuteAsync<T>(connection, queryOptions, options, ctx);
    }

    private static async Task<QueryResult<object>> ExecuteAsync<T>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        FlexQueryExecutionContext? ctx)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;

        await ConnectionHelper.EnsureOpenAsync(connection, ct);

        var dialect = SqlDialectResolver.Resolve(connection);
        var registry = options.Model?.Registry
            ?? FlexQueryDapper.DefaultModel?.Registry
            ?? new MappingRegistry();

        // Re-stamped here (in addition to RunAsync, above) because it's read again
        // right below via GetMapping(typeof(T)) — kept exactly as in the original
        // to avoid guessing whether Normalize() preserves Options.Items. Worth
        // confirming whether one of the two assignments is actually redundant.
        queryOptions.Items[ContextKeys.EntityType] = typeof(T);

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(queryOptions);
        var mapping = registry.GetMapping(typeof(T));

        var parameters = CommandParameterAdapter.ToDynamicParameters(command);
        
        var queryParameters = 
            command.Parameters.Select(p => new QueryParameter(p.Key, p.Value)).ToList().AsReadOnly();

        await ctx.NotifyTranslatedAsync(command.Sql, queryParameters);

        IReadOnlyList<object> HydrateIncludes(IEnumerable<dynamic> dynamicItems)
        {
            var hydrated = queryOptions.Expand?.Count > 0
                ? DapperRowHydrator.HydrateFilteredIncludes<T>(dynamicItems, mapping, registry, queryOptions.Expand)
                : DapperRowHydrator.HydrateIncludes<T>(dynamicItems, mapping, registry, queryOptions.Includes!);
            return hydrated.Cast<object>().ToList();
        }
        
        var rows = await connection.QueryAsync(
            command.Sql,
            parameters,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text);

        var items = DapperResultMaterializer.Materialize(
            rows,
            queryOptions,
            hydrateIncludes: HydrateIncludes,
            cancellationToken: ct);

        
        var (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(connection, queryOptions, translator, command, parameters, options);
        
        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(connection, queryOptions, translator, options, ct);

        await ctx.NotifyExecutedAsync(items.Count);
        
        var queryResult = queryOptions.BuildQueryResult(data: items, totalCount, aggregates: grandTotals, resultCount);
        
        await ctx.NotifyMaterializedAsync(queryResult);

        return queryResult;
    }
}