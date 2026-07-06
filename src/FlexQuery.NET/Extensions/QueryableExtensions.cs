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
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options to apply.</param>
    /// <returns>The filtered, sorted, and paged queryable.</returns>
    public static IQueryable<T> Apply<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.Apply(query, options);

    /// <summary>Applies only the filter predicate.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing the filter.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyFilter(query, options);

    /// <summary>Applies only the sort instructions.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing the sort instructions.</param>
    /// <returns>The sorted queryable.</returns>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySort(query, options);

    /// <summary>Applies only paging (skip/take).</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing paging parameters.</param>
    /// <returns>The paged queryable.</returns>
    public static IQueryable<T> ApplyPaging<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyPaging(query, options);

    /// <summary>
    /// Applies projection (select) dynamically and returns an IQueryable of objects.
    /// If no select is specified, returns the original query cast to object.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing projection settings.</param>
    /// <returns>A queryable of projected objects.</returns>
    public static IQueryable<object> ApplySelect<T>(
        this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySelect(query, options);

    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
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

        var options = parameters.ToQueryOptions();

        ValidatePaginationMode(options);

        options = options.Normalize();
        options.ValidateOrThrow<T>(exec);

        var hasProjection = options.HasProjection()
            || (options.Includes?.Count ?? 0) > 0
            || (options.Expand?.Count ?? 0) > 0;

        return query.ApplyFlexQuery(options, hasProjection, exec);

    }

    /// <summary>
    /// Parses a <see cref="QueryOptions"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
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

        ValidatePaginationMode(options);

        options = options.Normalize();
        options.ValidateOrThrow<T>(exec);

        var hasProjection = options.HasProjection()
            || (options.Includes?.Count ?? 0) > 0
            || (options.Expand?.Count ?? 0) > 0;

        return query.ApplyFlexQuery(options, hasProjection, exec);

    }

    private static void ValidatePaginationMode(QueryOptions options)
    {
        if (!options.IsKeysetMode) return;

        if (options.OffsetExplicitlyRequested)
        {
            throw new QueryValidationException(
                "Offset pagination parameters cannot be used together with Keyset Pagination. " +
                "Choose either Offset Pagination or Keyset Pagination.");
        }
    }

    private static QueryResult<object> ApplyFlexQuery<T>(this IQueryable<T> query, QueryOptions options, bool hasProjection, QueryExecutionOptions? execOptions = null)
    {
        var filtered = ApplyFilter(query, options);
        if (options.Distinct == true)
            filtered = Queryable.Distinct(filtered);

        filtered = ApplySort(filtered, options);
        var total = filtered.TryGetTotalCount(options, execOptions);

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

        filtered = options.IsKeysetMode
            ? QueryBuilder.ApplyKeysetPaging(filtered, options)
            : QueryBuilder.ApplyPaging(filtered, options);

        if (hasProjection)
        {
            var data = QueryBuilder.ApplySelect(filtered, options).ToList();
            return options.BuildQueryResult(data, total, grandTotals);
        }

        return options.BuildQueryResult(filtered.ToList(), total, grandTotals).ToObjectResult();
    }

    /// <summary>
    /// Applies both filtering and sorting to the query.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static IQueryable<T> ApplyFilterAndSort<T>(this IQueryable<T> query, QueryOptions options)
    {
        query = ApplyFilter(query, options);
        query = ApplySort(query, options);
        return query;
    }

    private static int? TryGetTotalCount<T>(
        this IQueryable<T> filteredQuery, QueryOptions options, QueryExecutionOptions? execOptions = null)
    {
        if (options.IsKeysetMode)
        {
            return options.IncludeCount == true ? filteredQuery.Count() : null;
        }

        return (options.IncludeCount == true && (execOptions?.IncludeTotalCount ?? true))
            ? filteredQuery.Count() 
            : null;
    }
}

