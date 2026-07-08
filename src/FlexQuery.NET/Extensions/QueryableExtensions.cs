using FlexQuery.NET.Builders;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET;

/// <summary>
/// Fluent extension methods on <see cref="IQueryable{T}"/> for applying
/// <see cref="QueryOptions"/>.
/// </summary>
public static class QueryableExtensions
{
    private static readonly FlexQueryProcessor Processor = new(new FlexQueryOptions());
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
        return Processor.ExecuteAsync(query, options, exec)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Parses a <see cref="QueryOptions"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="queryOptions">The query options.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    public static QueryResult<object> FlexQuery<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        Action<QueryExecutionOptions>? configure = null)
    {
        var exec = new QueryExecutionOptions();
        configure?.Invoke(exec);
        
        return Processor.ExecuteAsync(query, queryOptions, exec)
            .GetAwaiter()
            .GetResult();
        
    }
    
}
