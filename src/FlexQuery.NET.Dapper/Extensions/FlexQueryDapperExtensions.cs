using System.Data.Common;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Execution;
using FlexQuery.NET.Dapper.Materialization;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Utilities;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Execution;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Extension methods for executing FlexQuery operations against a <see cref="System.Data.Common.DbConnection"/>
/// using Dapper as the materialization engine. Provides overloads accepting <see cref="FlexQueryParameters"/>,
/// raw query-string dictionaries, or pre-parsed <see cref="QueryOptions"/>.
/// </summary>
/// <remarks>
/// <para>Cancellation is observed at the following stages:</para>
/// <list type="bullet">
///   <item><term>Connection open</term> <description>via <see cref="DbConnection.OpenAsync(System.Threading.CancellationToken)"/></description></item>
///   <item><term>Diagnostics events</term> <description>listener callbacks (<see cref="IFlexQueryExecutionListener"/>)</description></item>
///   <item><term>Result materialization</term> <description>a single check before the synchronous materialization loop</description></item>
/// </list>
/// <para>Note: Dapper's <c>QueryAsync</c> does not accept a <see cref="System.Threading.CancellationToken"/>.
/// The token is threaded through connection open, diagnostics, and materialization stages,
/// but not into the Dapper query execution itself. A future version may support this via
/// <c>CommandDefinition</c>.</para>
/// </remarks>
public static class FlexQueryDapperExtensions
{
    private static void SetEntityType(QueryOptions options, Type entityType)
    {
        options.Items[ContextKeys.EntityType] = entityType;
    }

    /// <summary>
    /// Parses <paramref name="parameters"/>, applies Dapper-specific options, and executes the query
    /// against the database connection, returning a paged result set.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user query parameters.</param>
    /// <param name="dapperQueryOptions">Optional delegate to configure Dapper-specific options (dialect, mapping registry, etc.).</param>
    /// <param name="configureExecution">Optional delegate to configure execution listeners.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? dapperQueryOptions = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        dapperQueryOptions?.Invoke(dapperOptions);

        var options = parameters.ToQueryOptions();
        var (validatedOptions, ctx) = await PrepareQueryInternalAsync<T>(
            connection, options, dapperOptions, configureExecution, cancellationToken);

        return await ExecuteQueryAsync<T>(connection, validatedOptions, dapperOptions, ctx);
    }

    /// <summary>
    /// Parses a raw dictionary of query-string values (e.g., from <c>IQueryCollection</c>), converts them
    /// into <see cref="FlexQueryParameters"/>, and executes the query against the connection.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">Raw query-string key/value pairs (e.g., filter, sort, select, page, pageSize).</param>
    /// <param name="dapperQueryOptions">Optional delegate to configure Dapper-specific options.</param>
    /// <param name="configureExecution">Optional delegate to configure execution listeners.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        Action<DapperQueryOptions>? dapperQueryOptions = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null,
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
            RawParameters = dict
        };

        return await FlexQueryAsync<T>(connection, flexParams, dapperQueryOptions, configureExecution, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against the connection with full control over
    /// Dapper options and execution configuration, returning a paged result set.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="options">Pre-parsed query options.</param>
    /// <param name="dapperQueryOptions">Dapper-specific execution options (dialect, mapping, security rules, etc.).</param>
    /// <param name="configureExecution">Optional delegate to configure execution listeners.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions options,
        DapperQueryOptions? dapperQueryOptions = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        var (validatedOptions, ctx) = await PrepareQueryInternalAsync<T>(
            connection, options, dapperOptions, configureExecution, cancellationToken);

        return await ExecuteQueryAsync<T>(connection, validatedOptions, dapperOptions, ctx);
    }
    
    /// <summary>
    /// Main execution pipeline: opens connection, translates query, materialises results,
    /// retrieves counts and aggregates, and builds the final <see cref="QueryResult{object}"/>.
    /// </summary>
    private static async Task<QueryResult<object>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions dapperOptions,
        FlexQueryExecutionContext? ctx = null) where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;
        await ConnectionHelper.EnsureOpenAsync(connection, ct);

        var dialect = dapperOptions.Dialect ?? SqlDialectResolver.Resolve(connection);
        var registry = dapperOptions.Model?.Registry ?? new MappingRegistry();
        options.Items[ContextKeys.EntityType] = typeof(T);

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);
        var parameters = CommandParameterAdapter.CreateParametersFromCommand(command);

        await FireTranslationEventAsync(ctx, command, ct);

        var items = await  QueryMaterializer.MaterializeResultsAsync<T>(connection, command, parameters, options, registry, dapperOptions, ct);

        int? totalCount = null;
        int? resultCount = null;
        if (dapperOptions.IncludeTotalCount && (options.IncludeCount ?? true))
        {
            (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(connection, options, translator, command, parameters, dapperOptions);
        }

        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(connection, options, translator, dapperOptions, ct);

        var now = DateTimeOffset.UtcNow;
        if (ctx?.Listener is not null)
        {
            await ctx.Listener.QueryExecutedAsync(
                new QueryExecutedEvent(ctx.QueryId, items.Count, null, ctx.Stopwatch.Elapsed, now),
                ctx.CancellationToken);
        }
        
        var queryResult = options.BuildQueryResult(items, totalCount, grandTotals, resultCount);

        if (ctx?.Listener is not null)
        {
            await ctx.Listener.QueryMaterializedAsync(
                new QueryMaterializedEvent(ctx.QueryId, queryResult, null, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        return queryResult;
    }
    
    /// <summary>
    /// Normalized and validates options, sets entity type, and fires the <c>QueryParsed</c> event
    /// if a listener is configured. Returns the validated options and an optional execution context.
    /// </summary>
    private static async Task<(QueryOptions options, FlexQueryExecutionContext? context)> PrepareQueryInternalAsync<T>(
            DbConnection connection,
            QueryOptions options,
            DapperQueryOptions dapperOptions,
            Action<FlexQueryExecutionConfig>? configureExecution,
            CancellationToken cancellationToken) where T : class
    {
        SetEntityType(options, typeof(T));
        options = options.Normalize();
        options.ValidateOrThrow(typeof(T), dapperOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is null) return (options, ctx);
        ctx = new FlexQueryExecutionContext(execConfig, cancellationToken);
        
        if (ctx.Listener != null)
        {
            await ctx.Listener.QueryParsedAsync(
                new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        return (options, ctx);
    }
    
    private static async Task FireTranslationEventAsync(
        FlexQueryExecutionContext? ctx, SqlCommand command, CancellationToken ct)
    {
        if (ctx?.Listener is null) return;

        var queryParameters = command.Parameters
            .Select(p => new QueryParameter(p.Key, p.Value))
            .ToList()
            .AsReadOnly();

        await ctx.Listener.QueryTranslatedAsync(
            new QueryTranslatedEvent(ctx.QueryId, command.Sql, queryParameters,
                ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
            ct);
    }

    private static string ExtractCountSql(string sql)
    {
        return SqlCountBuilder.ExtractCountSql(sql);
    }
}
