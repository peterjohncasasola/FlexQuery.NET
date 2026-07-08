using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET;

/// <summary>
/// Provides a simple entry point for executing FlexQuery operations without
/// using dependency injection.
///
/// <para>
/// This class uses a shared default <see cref="IFlexQueryProcessor"/> instance
/// configured with the default <see cref="FlexQueryOptions"/>. Applications
/// requiring custom configuration should register FlexQuery through dependency
/// injection and resolve <see cref="IFlexQueryProcessor"/> instead.
/// </para>
/// </summary>
public static class FlexQuery
{
    private static readonly FlexQueryOptions DefaultOptions = new();
    private static readonly IFlexQueryProcessor DefaultProcessor = new FlexQueryProcessor(DefaultOptions);

    /// <summary>
    /// Executes a FlexQuery request against the specified queryable using the
    /// default execution options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="queryOptions">The query options to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    public static Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        CancellationToken ct = default)
    {
        return DefaultProcessor.ExecuteAsync(query, queryOptions, ct);
    }

    /// <summary>
    /// Executes a FlexQuery request against the specified queryable using the
    /// supplied execution options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="queryOptions">The query options to execute.</param>
    /// <param name="options">The execution options that control server-side behavior.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    public static Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        QueryExecutionOptions options,
        CancellationToken ct = default)
    {
        return DefaultProcessor.ExecuteAsync(query, queryOptions, options, ct);
    }
}