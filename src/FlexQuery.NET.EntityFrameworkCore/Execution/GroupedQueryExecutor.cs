using FlexQuery.NET.Builders;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.EntityFrameworkCore.Execution;

/// <summary>
/// Executes the GROUP BY branch of the FlexQueryAsync pipeline: builds the grouped
/// query, counts and materializes it, and reports translate/execute/materialize
/// events to the listener, if any.
/// </summary>
internal static class GroupedQueryExecutor
{
    public static async Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> filtered,
        QueryOptions queryOptions,
        int? total,
        EfCoreQueryOptions? options,
        FlexQueryExecutionContext? ctx,
        CancellationToken cancellationToken)
        where T : class
    {
        var groupedQuery = GroupByBuilder.ApplyUntyped(filtered, queryOptions);

        var (sql, queryParameters) = SqlQueryInspector.TryGetSqlWithParameters(groupedQuery);

        await ctx.NotifyTranslatedAsync(sql, queryParameters);

        var resultCount = options?.IncludeTotalCount == true
            ? await GroupedQueryMaterializer.Count(groupedQuery, cancellationToken)
            : (int?)null;

        var data = await GroupedQueryMaterializer.Execute(groupedQuery, queryOptions, cancellationToken);
        var result = queryOptions.BuildQueryResult(data, total, resultCount: resultCount);

        await ctx.NotifyExecutedAsync(data.Count);
        await ctx.NotifyMaterializedAsync(result);

        return result;
    }
}