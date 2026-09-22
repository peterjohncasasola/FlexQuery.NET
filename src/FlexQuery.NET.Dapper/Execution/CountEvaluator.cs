using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Context;
using FlexQuery.NET.Dapper.Diagnostics;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Logging;

namespace FlexQuery.NET.Dapper.Execution;

internal static class CountEvaluator
{
    public static async Task<(int? totalCount, int? resultCount)> GetCountsAsync(
        DbConnection connection,
        QueryOptions queryOptions,
        SqlTranslator translator,
        SqlCommand mainCommand,
        DynamicParameters? mainParams,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default,
        ILogger? sqlLogger = null)
    {
        
        var shouldIncludeCount = options.IncludeTotalCount && (queryOptions.IncludeCount ?? true);
        if (!shouldIncludeCount) return (null, null);

        var sourceCountCommand = translator.TranslateSourceCount(queryOptions);
        var sourceCountParameters = CommandParameterAdapter.ToDynamicParameters(sourceCountCommand);

        DapperSqlLog.Command(sqlLogger, sourceCountCommand.Sql, sourceCountCommand.Parameters);

        var totalCount = (int)await connection.QuerySingleAsync<long>(new CommandDefinition(
            sourceCountCommand.Sql,
            sourceCountParameters,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        var resultCount = queryOptions.GroupBy is { Count: > 0 } || queryOptions.Distinct == true
            ? (int)await connection.QuerySingleAsync<long>(new CommandDefinition(
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql),
                mainParams!,
                commandTimeout: options.CommandTimeout,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))
            : totalCount;

        if (resultCount != totalCount)
        {
            DapperSqlLog.Command(
                sqlLogger,
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql),
                mainCommand.Parameters);
        }

        return (totalCount, resultCount);
    }
    
    
    public static async Task<(int? totalCount, int? resultCount)> GetCountsAsync(DapperQueryContext dapperQueryContext)
    {
        var options = dapperQueryContext.DapperQueryOptions;
        var translator = dapperQueryContext.SqlTranslator;
        var queryOptions = dapperQueryContext.QueryOptions;
        var sqlLogger = dapperQueryContext.SqlLogger;
        var connection = dapperQueryContext.Connection;
        var cancellationToken = dapperQueryContext.CancellationToken;

        var mainCommand = dapperQueryContext.SqlCommand!;
        var mainParams = dapperQueryContext.Parameters;
        
        var shouldIncludeCount = options.IncludeTotalCount && (queryOptions.IncludeCount ?? true);
        if (!shouldIncludeCount) return (null, null);

        var sourceCountCommand = translator.TranslateSourceCount(queryOptions);
        var sourceCountParameters = CommandParameterAdapter.ToDynamicParameters(sourceCountCommand);

        DapperSqlLog.Command(sqlLogger, sourceCountCommand.Sql, sourceCountCommand.Parameters);
        

        var totalCount = (int)await connection.QuerySingleAsync<long>(new CommandDefinition(
            sourceCountCommand.Sql,
            sourceCountParameters,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        var resultCount = queryOptions.GroupBy is { Count: > 0 } || queryOptions.Distinct == true
            ? (int)await connection.QuerySingleAsync<long>(new CommandDefinition(
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql),
                mainParams!,
                commandTimeout: options.CommandTimeout,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))
            : totalCount;

        if (resultCount != totalCount)
        {
            DapperSqlLog.Command(
                sqlLogger,
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql),
                mainCommand.Parameters);
        }

        return (totalCount, resultCount);
    }
}
