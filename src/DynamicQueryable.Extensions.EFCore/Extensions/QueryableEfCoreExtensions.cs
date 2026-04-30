using DynamicQueryable.Builders;
using DynamicQueryable.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicQueryable.Extensions.EFCore;

/// <summary>
/// EF Core-specific async extensions for materializing query results.
/// </summary>
public static class QueryableEfCoreExtensions
{
    /// <summary>
    /// Async variant of <c>ToQueryResult</c> for EF Core query providers.
    /// </summary>
    public static async Task<QueryResult<T>> ToQueryResultAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        CancellationToken cancellationToken = default)
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        var filtered = QueryBuilder.ApplyFilter(query, options);
        filtered = QueryBuilder.ApplySort(filtered, options);

        var total = await filtered.CountAsync(cancellationToken);

        var paged = QueryBuilder.ApplyPaging(filtered, options);
        var data = await paged.ToListAsync(cancellationToken);

        return new QueryResult<T>
        {
            TotalCount = total,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Data = data
        };
    }

    /// <summary>
    /// Async projected result variant using options-driven selection.
    /// </summary>
    public static async Task<QueryResult<object>> ToProjectedQueryResultAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        CancellationToken cancellationToken = default)
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        var filtered = QueryBuilder.ApplyFilter(query, options);
        filtered = QueryBuilder.ApplySort(filtered, options);

        var total = await filtered.CountAsync(cancellationToken);

        var paged = QueryBuilder.ApplyPaging(filtered, options);
        var data = await QueryBuilder.ApplySelect(paged, options).ToListAsync(cancellationToken);

        return new QueryResult<object>
        {
            TotalCount = total,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Data = data
        };
    }
}
