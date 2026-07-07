using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Dapper;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Projection;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Materialization;
using FlexQuery.NET.Dapper.Options;
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

        var parsedOptions = parameters.ToQueryOptions();
        SetEntityType(parsedOptions, typeof(T));

        parsedOptions = parsedOptions.Normalize();
        parsedOptions.ValidateOrThrow(typeof(T), dapperOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, cancellationToken);
            if (ctx.Listener != null)
            {
                await ctx.Listener.QueryParsedAsync(
                    new QueryParsedEvent(ctx.QueryId, parsedOptions, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }
        }

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions, ctx);
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

        SetEntityType(options, typeof(T));

        options = options.Normalize();
        options.ValidateOrThrow(typeof(T), dapperOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, cancellationToken);
            if (ctx.Listener != null)
            {
                await ctx.Listener.QueryParsedAsync(
                    new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                    ctx.CancellationToken);
            }
        }

        return await ExecuteQueryAsync<T>(connection, options, dapperOptions, ctx);
    }

    private static async Task<QueryResult<object>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions dapperQueryOptions,
        FlexQueryExecutionContext? ctx = null) where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;

        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(ct);
        }

        var dialect = dapperQueryOptions.Dialect ?? SqlDialectResolver.Resolve(connection);

        var registry = dapperQueryOptions.Model?.Registry ?? new MappingRegistry();
        options.Items[ContextKeys.EntityType] = typeof(T);

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);
        var entityType = typeof(T);
        var mapping = registry.GetMapping(entityType);

        var parameters = new DynamicParameters();
        foreach (var param in command.Parameters)
        {
            var cleanName = param.Key.TrimStart('@', ':', '?');
            parameters.Add(cleanName, param.Value);
        }

        // Emit translation event
        if (ctx?.Listener is not null)
        {
            var queryParameters = command.Parameters
                .Select(p => new QueryParameter(p.Key, p.Value))
                .ToList()
                .AsReadOnly();
            await ctx.Listener.QueryTranslatedAsync(
                new QueryTranslatedEvent(ctx.QueryId, command.Sql, queryParameters, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        var metadata = ProjectionMetadataBuilder.Build(entityType, options);
        var useDynamicType = false;

        IReadOnlyList<object> items;
        if (options.Includes?.Count > 0 || options.Expand?.Count > 0)
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            IReadOnlyList<T> hydrated;
            if (options.Expand?.Count > 0)
            {
                hydrated = DapperRowHydrator.HydrateFilteredIncludes<T>(dynamicItems, mapping, registry, options.Expand);
            }
            else
            {
                hydrated = DapperRowHydrator.HydrateIncludes<T>(dynamicItems, mapping, registry, options.Includes!);
            }
            items = hydrated.Cast<object>().ToList();
        }
        else if (useDynamicType)
        {
            var projectedType = DynamicTypeBuilder.GetDynamicType(
                new Dictionary<string, Type>(metadata.FieldTypes));

            var dynamicItems = await connection.QueryAsync(
                projectedType,
                command.Sql,
                parameters,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            items = dynamicItems.ToList();
        }
        else if (typeof(T) == typeof(object) || options.GroupBy?.Count > 0 || options.Aggregates.Count > 0)
        {
            var useDynamicForGrouping = options.GroupBy?.Count > 0 || options.Aggregates.Count > 0;

            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            if (useDynamicForGrouping)
            {
                var rows = dynamicItems
                    .Select(d => (IDictionary<string, object>)d)
                    .ToList();

                if (rows.Count == 0)
                {
                    items = [];
                }
                else
                {
                    var colTypes = rows[0].Keys
                        .ToDictionary(k => k, _ => typeof(object), StringComparer.OrdinalIgnoreCase);
                    var projectedType = DynamicTypeBuilder.GetDynamicType(
                        new Dictionary<string, Type>(colTypes));

                    ct.ThrowIfCancellationRequested();

                    items = rows.Select(row =>
                    {
                        var instance = Activator.CreateInstance(projectedType)!;
                        foreach (var kvp in row)
                        {
                            var prop = projectedType.GetProperty(kvp.Key);
                            if (prop is { CanWrite: true })
                                prop.SetValue(instance, kvp.Value);
                        }
                        return instance;
                    }).ToList();
                }
            }
            else
            {
                items = dynamicItems
                    .Select(d => (object)new Dictionary<string, object>((IDictionary<string, object>)d, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);
            items = dynamicItems
                .Select(object (d) => new Dictionary<string, object>((IDictionary<string, object>)d, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var shouldIncludeCount = dapperQueryOptions.IncludeTotalCount && (options.IncludeCount ?? true);

        int? totalCount = null;
        int? resultCount = null;
        if (shouldIncludeCount)
        {
            var sourceCountCommand = translator.TranslateSourceCount(options);
            var sourceCountParameters = new DynamicParameters();
            foreach (var param in sourceCountCommand.Parameters)
            {
                var cleanName = param.Key.TrimStart('@', ':', '?');
                sourceCountParameters.Add(cleanName, param.Value);
            }

            totalCount = (int)await connection.QuerySingleAsync<long>(
                sourceCountCommand.Sql,
                sourceCountParameters,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            resultCount = options.GroupBy is { Count: > 0 } || options.Distinct == true
                ? (int)await connection.QuerySingleAsync<long>(
                    ExtractCountSql(command.Sql),
                    parameters,
                    commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                    commandType: CommandType.Text)
                : totalCount;
        }

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (options.Aggregates.Count > 0 && (options.GroupBy == null || options.GroupBy.Count == 0))
        {
            var aggCommand = translator.TranslateAggregates(options);
            var aggParams = new DynamicParameters();
            foreach (var param in aggCommand.Parameters)
            {
                var cleanName = param.Key.TrimStart('@', ':', '?');
                aggParams.Add(cleanName, param.Value);
            }

            var aggResult = await connection.QueryFirstOrDefaultAsync(
                aggCommand.Sql,
                aggParams,
                commandTimeout: dapperQueryOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            if (aggResult != null)
            {
                grandTotals = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
                var rowDict = (IDictionary<string, object>)aggResult;
                foreach (var agg in options.Aggregates)
                {
                    if (rowDict.TryGetValue(agg.Alias, out var val))
                    {
                        var fieldName = agg.Field ?? "all";
                        var fnName = agg.Function;
                        if (!grandTotals.TryGetValue(fieldName, out var fnDict))
                        {
                            fnDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            grandTotals[fieldName] = fnDict;
                        }
                        fnDict[fnName] = val;
                    }
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        if (ctx?.Listener is not null)
        {
            await ctx.Listener.QueryExecutedAsync(
                new QueryExecutedEvent(ctx.QueryId, items.Count, null, ctx.Stopwatch.Elapsed, now),
                ctx.CancellationToken);
        }

        var queryResult = new QueryResult<object>
        {
            Data = items,
            TotalCount = totalCount,
            ResultCount = resultCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Aggregates = grandTotals
        };

        if (ctx?.Listener is not null)
        {
            await ctx.Listener.QueryMaterializedAsync(
                new QueryMaterializedEvent(ctx.QueryId, queryResult, null, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        return queryResult;
    }

    private static string ExtractCountSql(string sql)
    {
        var patterns = new[] { @"\bORDER\s+BY\b", @"\bLIMIT\b", @"\bOFFSET\b" };
        var minIdx = sql.Length;

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
            while (match.Success)
            {
                if (!IsInsideParentheses(sql, match.Index))
                {
                    if (match.Index < minIdx)
                        minIdx = match.Index;
                    break;
                }
                match = match.NextMatch();
            }
        }

        var baseSql = sql[..minIdx];
        return $"SELECT COUNT(1) FROM ({baseSql.Trim()}) AS CountTable";
    }

    private static bool IsInsideParentheses(string sql, int index)
    {
        int depth = 0;
        for (int i = 0; i < index; i++)
        {
            if (sql[i] == '(')
                depth++;
            else if (sql[i] == ')')
                depth--;
        }
        return depth > 0;
    }
}
