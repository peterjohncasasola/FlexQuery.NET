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
        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items["EntityType"] = typeof(T);

        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);

        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow<T>(execOptions);

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
        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items["EntityType"] = typeof(T);

        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
       
        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow<T>(execOptions);

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
        var flexParams = new FlexQueryParameters
        {
            Filter = parameters.TryGetValue("filter", out var filter) ? filter.ToString() : null,
            Sort = parameters.TryGetValue("sort", out var sort) ? sort.ToString() : null,
            Select = parameters.TryGetValue("select", out var select) ? select.ToString() : null,
            Page = parameters.TryGetValue("page", out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = parameters.TryGetValue("pageSize", out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null
        };

        return await FlexQueryAsync<T>(connection, flexParams, configureDapper);
    }

    private static async Task<QueryResult<T>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions execOptions) where T : class
    {
        var dialect = execOptions.Dialect 
            ?? DapperQueryOptions.GlobalDefaultDialect 
            ?? DapperQueryOptions.GlobalDialectResolver.Resolve(connection);
        var translator = new SqlTranslator(new Mapping.MappingRegistry(), dialect);
        var command = translator.Translate(options);

        var parameters = new DynamicParameters();
        foreach (var param in command.Parameters)
        {
            // Strip dialect-specific parameter prefixes (@, :, ?) for Dapper's DynamicParameters
            var cleanName = param.Key.TrimStart('@', ':', '?');
            parameters.Add(cleanName, param.Value);
        }

        var items = (await connection.QueryAsync<T>(
            command.Sql,
            parameters,
            commandTimeout: execOptions.CommandTimeoutSeconds,
            commandType: CommandType.Text)).AsList();

        var totalCount = items.Count;
        if (execOptions.IncludeTotalCount && options.Paging.Page > 1)
        {
            var countSql = ExtractCountSql(command.Sql);
            totalCount = (int)await connection.QuerySingleAsync<long>(countSql, parameters, commandTimeout: execOptions.CommandTimeoutSeconds, commandType: CommandType.Text);
        }

        return new QueryResult<T>
        {
            Data = items,
            TotalCount = totalCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize
        };
    }

    private static string ExtractCountSql(string sql)
    {
        var idx = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        var baseSql = idx >= 0 ? sql[..idx] : sql;
        return $"SELECT COUNT(1) FROM ({baseSql.Trim()}) AS CountTable";
    }
}
