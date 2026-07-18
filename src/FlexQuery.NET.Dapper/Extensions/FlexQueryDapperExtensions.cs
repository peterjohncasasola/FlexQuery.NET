using System.Data.Common;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Execution;
using FlexQuery.NET.Dapper.Options;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Extension methods for executing FlexQuery requests against a
/// <see cref="DbConnection"/> using Dapper.
/// Provides overloads accepting <see cref="FlexQueryParameters"/>,
/// raw query-string dictionaries, or pre-parsed <see cref="QueryOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Cancellation is observed during connection opening, diagnostics callbacks,
/// and result materialization.
/// </para>
/// <para>
/// Dapper's <c>QueryAsync</c> APIs do not currently accept a
/// <see cref="CancellationToken"/>. As a result, cancellation cannot interrupt
/// the database query once execution has started. A future version may support
/// this through <c>CommandDefinition</c>.
/// </para>
/// </remarks>
public static class FlexQueryDapperExtensions
{
    /// <summary>
    /// Parses the supplied <paramref name="parameters"/> and executes the
    /// resulting FlexQuery request against the database connection.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">The query parameters.</param>
    /// <param name="configure">
    /// Optional delegate used to configure Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configure?.Invoke(dapperOptions);

        var effectiveSyntax = dapperOptions.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var options = parameters.ToQueryOptions(effectiveSyntax);
        return await DapperQueryExecutor.RunAsync<T>(connection, options, dapperOptions, cancellationToken);
    }

    /// <summary>
    /// Converts raw query-string values into
    /// <see cref="FlexQueryParameters"/> and executes the query.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">The raw query-string key/value pairs.</param>
    /// <param name="configure">
    /// Optional delegate used to configure Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var dict = parameters.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var flexParams = new FlexQueryParameters
        {
            Filter = dict.GetValueOrDefault(QueryOptionKeys.Filter) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Filter}"),
            Sort = dict.GetValueOrDefault(QueryOptionKeys.Sort) ?? dict.GetValueOrDefault(QueryOptionKeys.OrderBy) ?? dict.GetValueOrDefault($"${QueryOptionKeys.OrderBy}"),
            Select = dict.GetValueOrDefault(QueryOptionKeys.Select) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Select}"),
            Include = dict.GetValueOrDefault(QueryOptionKeys.Include) ?? dict.GetValueOrDefault(QueryOptionKeys.Expand) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Expand}"),
            Page = dict.TryGetValue(QueryOptionKeys.Page, out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = dict.TryGetValue(QueryOptionKeys.PageSize, out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null,
            PreserveRawOrder = true
        };

        return await FlexQueryAsync<T>(connection, flexParams, configure, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against the
    /// database connection.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="queryOptions">The pre-parsed query options.</param>
    /// <param name="options">
    /// Optional Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="connection"/> or
    /// <paramref name="queryOptions"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(queryOptions);

        var dapperOptions = options ?? new DapperQueryOptions();

        return await DapperQueryExecutor.RunAsync<T>(connection, queryOptions, dapperOptions, cancellationToken);
    }
}