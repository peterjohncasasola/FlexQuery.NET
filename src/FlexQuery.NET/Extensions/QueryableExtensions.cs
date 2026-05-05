using FlexQuery.NET.Builders;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using System.ComponentModel;

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
    [Obsolete("ApplyQueryOptions is deprecated and will be removed in v3. " +
    "Use FlexQuery(...) for the unified query pipeline (filtering, sorting, and paging).")]
    [EditorBrowsable(EditorBrowsableState.Never)]
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
    [Obsolete("ToProjectedQueryResult is deprecated and will be removed in v3. " +
    "Use FlexQuery(...) for the unified query pipeline (filtering, sorting, projection, and paging).")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static QueryResult<object> ToProjectedQueryResult<T>(
        this IQueryable<T> query,
        QueryOptions options)
    {
        var filtered = ApplyFilterAndSort(query, options);

        var total = filtered.TryGetTotalCount(options);

        var paged = QueryBuilder.ApplyPaging(filtered, options);

        var data = QueryBuilder.ApplySelect(paged, options);

        return options.BuildQueryResult(data, total);
    }

    /// <summary>
    /// Convenience: executes the query and wraps it in a <see cref="QueryResult{T}"/>
    /// with total count, page metadata, and the paged data.
    /// </summary>
    [Obsolete("ToQueryResult is deprecated and will be removed in v3. " +
    "Use FlexQuery(...) for the unified query pipeline (filtering, sorting, and paging).")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static QueryResult<T> ToQueryResult<T>(
        this IQueryable<T> query, QueryOptions options)
    {
        var filtered = ApplyFilterAndSort(query, options);

        var total = filtered.TryGetTotalCount(options);

        var data  = QueryBuilder.ApplyPaging(filtered, options);

        return options.BuildQueryResult(data, total);
    }

    /// <summary>
    /// Conditionally computes the total count if requested.
    /// </summary>
    private static int? TryGetTotalCount<T>(
        this IQueryable<T> filteredQuery, QueryOptions options)
    {
        return options.IncludeCount == true 
            ? filteredQuery.Count() 
            : null;
    }

    /// <summary>
    /// Applies both filtering and sorting to the query.
    /// </summary>
    internal static IQueryable<T> ApplyFilterAndSort<T>(this IQueryable<T> query, QueryOptions options)
    {
        query = ApplyFilter(query, options);
        query = ApplySort(query, options);
        return query;
    }

    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    public static QueryResult<object> FlexQuery<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        Action<QueryExecutionOptions>? configure = null)
    {
        var exec = new QueryExecutionOptions();
        configure?.Invoke(exec);

        var options = QueryOptionsParser.Parse(parameters);

        options.ValidateOrThrow<T>(exec);

        var hasProjection = options.HasProjection();

        return query.ApplyFlexQuery(options, hasProjection);

    }

    private static QueryResult<object> ApplyFlexQuery<T>(this IQueryable<T> query, QueryOptions options, bool hasProjection)
    {
        var filtered = ApplyFilterAndSort(query, options);
        var total = filtered.TryGetTotalCount(options);
        filtered = QueryBuilder.ApplyPaging(filtered, options);

        if (hasProjection)
        {
            var data = QueryBuilder.ApplySelect(filtered, options).ToList();
            return options.BuildQueryResult(data, total); // QueryResult<object>
        }

        //convert from QueryResult<T> to QueryResult<object> by treating each T as an object (no projection)
        return options.BuildQueryResult(filtered.ToList(), total).ToObjectResult();
    }
}
