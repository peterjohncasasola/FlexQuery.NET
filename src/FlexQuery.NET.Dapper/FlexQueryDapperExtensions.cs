using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Extensions;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Materialization;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Extension methods for executing FlexQuery requests with Dapper.
/// </summary>
public static class FlexQueryDapperExtensions
{
    /// <summary>
    /// Executes a FlexQuery using FlexQueryParameters with validation.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configureDapper = null) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);

        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? typeof(T);

        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions);
    }

    /// <summary>
    /// Executes a FlexQuery using FlexQueryParameters with full options.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        DapperQueryOptions? dapperQueryOptions = null) where T : class
    {
        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? typeof(T);
       
        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions);
    }

    /// <summary>
    /// Executes a FlexQuery using raw query string parameters.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        Action<DapperQueryOptions>? configureDapper = null) where T : class
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

        return await FlexQueryAsync<T>(connection, flexParams, configureDapper);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> using Dapper with validation.
    /// </summary>
    /// <remarks>
    /// Use this overload when composing with adapter packages (e.g. FlexQuery.NET.AgGrid,
    /// FlexQuery.NET.Parsers.MiniOData) that parse external formats into <see cref="QueryOptions"/>.
    /// <code>
    /// // Step 1: Parse (adapter package)
    /// var options = AgGridQueryOptionsParser.Parse(agGridRequest);
    ///
    /// // Step 2: Execute (Dapper package)
    /// var result = await connection.FlexQueryAsync&lt;User&gt;(options);
    /// </code>
    /// </remarks>
    /// <param name="connection">The database connection.</param>
    /// <param name="options">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="configureDapper">Optional configuration for Dapper-specific execution options.</param>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions options,
        Action<DapperQueryOptions>? configureDapper = null) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);

        return await connection.FlexQueryAsync<T>(options, dapperOptions);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> using Dapper with full options.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="options">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="dapperQueryOptions">Dapper-specific execution options.</param>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions options,
        DapperQueryOptions? dapperQueryOptions = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        options.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? typeof(T);

        var execOptions = dapperOptions.ToQueryExecutionOptions();

        options.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        return await ExecuteQueryAsync<T>(connection, options, dapperOptions);
    }

    private static async Task<QueryResult<T>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions execOptions) where T : class
    {
        var dialect = execOptions.Dialect 
            ?? DapperQueryOptions.GlobalDefaultDialect 
            ?? DapperQueryOptions.GlobalDialectResolver.Resolve(connection);
        
        var registry = execOptions.MappingRegistry ?? new Mapping.MappingRegistry();
        
        // Propagate EntityType to options for translator
        if (execOptions.EntityType != null)
            options.Items[ContextKeys.EntityType] = execOptions.EntityType;

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);
        var mapping = registry.GetMapping(execOptions.EntityType ?? typeof(T));

        var parameters = new DynamicParameters();
        foreach (var param in command.Parameters)
        {
            var cleanName = param.Key.TrimStart('@', ':', '?');
            parameters.Add(cleanName, param.Value);
        }

        IReadOnlyList<T> items;
        if (options.Includes?.Count > 0)
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            items = DapperRowHydrator.HydrateIncludes<T>(dynamicItems, mapping, registry, options.Includes);
        }
        else if (typeof(T) == typeof(object) || (options.Aggregates.Count > 0 && options.GroupBy?.Count > 0))
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);
            
            items = dynamicItems
                .Select(d => (T)(object)new Dictionary<string, object>((IDictionary<string, object>)d, StringComparer.OrdinalIgnoreCase))
                .AsList();
        }
        else
        {
            items = (await connection.QueryAsync<T>(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text)).AsList();
        }

        var totalCount = items.Count;
        if (execOptions.IncludeTotalCount && (options.Paging.Page > 1 || (options.Paging.PageSize > 0 && items.Count == options.Paging.PageSize)))
        {
            var countSql = ExtractCountSql(command.Sql);
            totalCount = (int)await connection.QuerySingleAsync<long>(countSql, parameters, commandTimeout: execOptions.CommandTimeoutSeconds, commandType: CommandType.Text);
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

        return new QueryResult<T>
        {
            Data = items,
            TotalCount = totalCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Aggregates = grandTotals
        };
    }

    private static string ExtractCountSql(string sql)
    {
        var keywords = new[] { "ORDER BY", "LIMIT", "OFFSET" };
        var minIdx = sql.Length;
        foreach (var kw in keywords)
        {
            var idx = sql.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < minIdx) minIdx = idx;
        }
        var baseSql = sql[..minIdx];
        return $"SELECT COUNT(1) FROM ({baseSql.Trim()}) AS CountTable";
    }
}
