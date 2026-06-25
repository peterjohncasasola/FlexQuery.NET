using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Dapper;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Projection;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Extensions;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Materialization;

namespace FlexQuery.NET.Dapper;

public static class FlexQueryDapperExtensions
{
    private static void ValidateEntityType(Type t, Type? entityType)
    {
        var resolved = entityType ?? t;
        if (resolved == typeof(object))
        {
            throw new InvalidOperationException(
                "Entity type could not be resolved. Set EntityType in DapperQueryOptions " +
                "or use a concrete type parameter when calling FlexQueryAsync<T>().");
        }
    }

    private static void SetEntityType(QueryOptions options, Type t, DapperQueryOptions dapperOptions)
    {
        options.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? t;
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configureDapper = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);
        ValidateEntityType(typeof(T), dapperOptions.EntityType);

        return await connection.FlexQueryAsync<T>(parameters, dapperOptions, configureExecution);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        DapperQueryOptions? dapperQueryOptions = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null) where T : class
    {
        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        ValidateEntityType(typeof(T), dapperOptions.EntityType);

        var parsedOptions = parameters.ToQueryOptions();
        SetEntityType(parsedOptions, typeof(T), dapperOptions);

        var execOptions = dapperOptions.ToQueryExecutionOptions();
        parsedOptions.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, default);
            await ctx.Listener.QueryParsedAsync(
                new QueryParsedEvent(ctx.QueryId, parsedOptions, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions, ctx);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        Action<DapperQueryOptions>? configureDapper = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null) where T : class
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

        return await FlexQueryAsync<T>(connection, flexParams, configureDapper, configureExecution);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions options,
        Action<DapperQueryOptions>? configureDapper = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);

        return await connection.FlexQueryAsync<T>(options, dapperOptions, configureExecution);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions options,
        DapperQueryOptions? dapperQueryOptions = null,
        Action<FlexQueryExecutionConfig>? configureExecution = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        ValidateEntityType(typeof(T), dapperOptions.EntityType);

        SetEntityType(options, typeof(T), dapperOptions);

        var execOptions = dapperOptions.ToQueryExecutionOptions();
        options.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        var execConfig = new FlexQueryExecutionConfig();
        configureExecution?.Invoke(execConfig);
        FlexQueryExecutionContext? ctx = null;
        if (execConfig.Listener is not null)
        {
            ctx = new FlexQueryExecutionContext(execConfig, default);
            await ctx.Listener.QueryParsedAsync(
                new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        return await ExecuteQueryAsync<T>(connection, options, dapperOptions, ctx);
    }

    private static async Task<QueryResult<object>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions execOptions,
        FlexQueryExecutionContext? ctx = null) where T : class
    {
        var ct = ctx?.CancellationToken ?? default;

        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(ct);
        }

        var dialect = execOptions.Dialect
            ?? DapperQueryOptions.GlobalDefaultDialect
            ?? DapperQueryOptions.GlobalDialectResolver.Resolve(connection);

        var registry = execOptions.MappingRegistry ?? new Mapping.MappingRegistry();

        if (execOptions.EntityType != null)
            options.Items[ContextKeys.EntityType] = execOptions.EntityType;

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);
        var entityType = execOptions.EntityType ?? typeof(T);
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
            var paramDict = command.Parameters.ToDictionary(k => k.Key, v => v.Value);
            await ctx.Listener.QueryTranslatedAsync(
                new QueryTranslatedEvent(ctx.QueryId, command.Sql, paramDict, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
                ctx.CancellationToken);
        }

        var metadata = ProjectionMetadataBuilder.Build(entityType, options);
        var useDynamicType = metadata.IsProjected
            && options.GroupBy == null
            && options.Aggregates.Count == 0
            && typeof(T) != typeof(object);

        IReadOnlyList<object> items;
        if (options.Includes?.Count > 0)
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            var hydrated = DapperRowHydrator.HydrateIncludes<T>(dynamicItems, mapping, registry, options.Includes);
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
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            items = dynamicItems.Cast<object>().ToList();
        }
        else if (typeof(T) == typeof(object) || options.GroupBy?.Count > 0 || options.Aggregates.Count > 0)
        {
            var useDynamicForGrouping = options.GroupBy?.Count > 0 || options.Aggregates.Count > 0;

            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
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
                    }).Cast<object>().ToList();
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
            items = (await connection.QueryAsync<T>(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text)).Cast<object>().ToList();
        }

        bool shouldIncludeCount = execOptions.IncludeTotalCount && (options.IncludeCount ?? true);

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
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            resultCount = options.GroupBy is { Count: > 0 } || options.Distinct == true
                ? (int)await connection.QuerySingleAsync<long>(
                    ExtractCountSql(command.Sql),
                    parameters,
                    commandTimeout: execOptions.CommandTimeoutSeconds,
                    commandType: CommandType.Text)
                : totalCount;
        }

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (options.Aggregates?.Count > 0 && (options.GroupBy == null || options.GroupBy.Count == 0))
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
                commandTimeout: execOptions.CommandTimeoutSeconds,
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
                        fnDict[fnName] = val ?? 0;
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
