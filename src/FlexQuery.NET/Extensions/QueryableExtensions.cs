using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;

namespace FlexQuery.NET;

/// <summary>
/// Fluent extension methods on <see cref="IQueryable{T}"/> for applying
/// <see cref="QueryOptions"/>.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies all query options (filter, sort, paging) and returns the shaped queryable.
    /// </summary>
    /// <remarks>
    /// If <see cref="QueryOptions.Select"/> is provided, use <see cref="ApplySelect{T}"/> on the result.
    /// </remarks>
    public static IQueryable<T> ApplyQueryOptions<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.Apply(query, options);

    /// <summary>Applies only the filter predicate.</summary>
    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyFilter(query, options);

    /// <summary>Applies only the sort instructions.</summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySort(query, options);

    /// <summary>Applies only paging (skip/take).</summary>
    public static IQueryable<T> ApplyPaging<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyPaging(query, options);

    /// <summary>
    /// Applies projection (select) dynamically and returns an IQueryable of objects.
    /// If no select is specified, returns the original query cast to object.
    /// </summary>
    public static IQueryable<object> ApplySelect<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySelect(query, options);

    /// <summary>
    /// Executes a query like <see cref="ToQueryResult{T}"/>, but returns projected rows.
    /// Uses <paramref name="options"/>.Select (and includes/JSON select if present) to shape the result.
    /// </summary>
    public static QueryResult<object> ToProjectedQueryResult<T>(
        this IQueryable<T> query,
        QueryOptions options)
    {
        var filtered = ApplyFilterAndSort(query, options);

        var total = filtered.Count();

        var paged = QueryBuilder.ApplyPaging(filtered, options);

        var data = QueryBuilder.ApplySelect(paged, options).ToList();

        return new QueryResult<object>
        {
            TotalCount = total,
            Page       = options.Paging.Page,
            PageSize   = options.Paging.PageSize,
            Data       = data
        };
    }

    /// <summary>
    /// Convenience: executes the query and wraps it in a <see cref="QueryResult{T}"/>
    /// with total count, page metadata, and the paged data.
    /// </summary>
    public static QueryResult<T> ToQueryResult<T>(
        this IQueryable<T> query, QueryOptions options)
    {
        var filtered = ApplyFilterAndSort(query, options);

        var total = filtered.Count();
        var data  = QueryBuilder.ApplyPaging(filtered, options).ToList();

        return new QueryResult<T>
        {
            TotalCount = total,
            Page       = options.Paging.Page,
            PageSize   = options.Paging.PageSize,
            Data       = data
        };
    }

    private static IQueryable<T> ApplyFilterAndSort<T>(IQueryable<T> query, QueryOptions options)
    {
        query = ApplyFilter(query, options);
        query = ApplySort(query, options);
        return query;
    }

}
