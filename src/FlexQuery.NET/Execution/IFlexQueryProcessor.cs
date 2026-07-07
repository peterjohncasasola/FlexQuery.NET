using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Execution;

/// <summary>
/// Defines the contract for executing FlexQuery.NET queries against an
/// <see cref="IQueryable{T}"/> data source.
/// </summary>
/// <remarks>
/// Implementations validate the supplied <see cref="QueryOptions"/>,
/// apply filtering, sorting, paging, and other supported query operations,
/// then return the results as a <see cref="QueryResult{T}"/>.
/// </remarks>
public interface IFlexQueryProcessor
{
    /// <summary>
    /// Executes the specified query using the configured default execution options.
    /// </summary>
    /// <typeparam name="T">The element type of the query.</typeparam>
    /// <param name="query">The queryable data source.</param>
    /// <param name="options">The query options to apply.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the query results.
    /// </returns>
    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Executes the specified query using the provided execution options.
    /// </summary>
    /// <typeparam name="T">The element type of the query.</typeparam>
    /// <param name="query">The queryable data source.</param>
    /// <param name="options">The query options to apply.</param>
    /// <param name="execOptions">
    /// The execution options that control query behavior.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the query results.
    /// </returns>
    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken ct = default);

    /// <summary>
    /// Executes the specified query using the provided execution configuration.
    /// </summary>
    /// <typeparam name="T">The element type of the query.</typeparam>
    /// <param name="query">The queryable data source.</param>
    /// <param name="options">The query options to apply.</param>
    /// <param name="config">
    /// The execution configuration used during query processing.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the query results.
    /// </returns>
    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        FlexQueryExecutionConfig config,
        CancellationToken ct = default);
}