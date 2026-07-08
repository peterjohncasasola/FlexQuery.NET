using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Execution;

/// <summary>
/// Defines the contract for executing FlexQueryAsync.NET queries against an
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
    /// <param name="queryOptions">The query options to apply.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the query results.
    /// </returns>
    Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        CancellationToken ct = default);

    /// <summary>
    /// Executes the specified query using the provided execution options.
    /// </summary>
    /// <typeparam name="T">The element type of the query.</typeparam>
    /// <param name="query">The queryable data source.</param>
    /// <param name="queryOptions">The query options to apply.</param>
    /// <param name="options">
    /// The execution options that control query behaviour.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the query results.
    /// </returns>
    Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        QueryExecutionOptions? options = null,
        CancellationToken ct = default);
}