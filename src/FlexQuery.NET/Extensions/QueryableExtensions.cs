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
    /// Applies filter, sort, and paging in sequence.
    /// </summary>
    public static IQueryable<T> Apply<T>(
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

    /// <summary>
    /// Parses a <see cref="QueryOptions"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    public static QueryResult<object> FlexQuery<T>(
        this IQueryable<T> query,
        QueryOptions options,
        Action<QueryExecutionOptions>? configure = null)
    {
        var exec = new QueryExecutionOptions();
        configure?.Invoke(exec);

        options.ValidateOrThrow<T>(exec);

        var hasProjection = options.HasProjection();

        return query.ApplyFlexQuery(options, hasProjection);

    }

    private static QueryResult<object> ApplyFlexQuery<T>(this IQueryable<T> query, QueryOptions options, bool hasProjection)
    {
        var filtered = ApplyFilterAndSort(query, options);
        var total = filtered.TryGetTotalCount(options);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;

        if (options.Aggregates.Count > 0 &&
            (options.GroupBy == null || options.GroupBy.Count == 0))
        {
            var aggregateQuery = GroupByBuilder.Apply(filtered, options);

            var aggRow = aggregateQuery.FirstOrDefault();

            grandTotals = AggregateResultBuilder.Build(
                aggRow,
                options.Aggregates);
        }

        filtered = QueryBuilder.ApplyPaging(filtered, options);

        if (hasProjection)
        {
            var data = QueryBuilder.ApplySelect(filtered, options).ToList();
            return options.BuildQueryResult(data, total, grandTotals); // QueryResult<object>
        }

        //convert from QueryResult<T> to QueryResult<object> by treating each T as an object (no projection)
        return options.BuildQueryResult(filtered.ToList(), total, grandTotals).ToObjectResult();
    }
}
