using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Sql.Utilities;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Execution;

internal static class CountEvaluator
{
    public static async Task<(int? totalCount, int? resultCount)> GetCountsAsync(
        DbConnection connection,
        QueryOptions options,
        SqlTranslator translator,
        SqlCommand mainCommand,
        DynamicParameters mainParams,
        DapperQueryOptions dapperOptions)
    {
        var sourceCountCmd = translator.TranslateSourceCount(options);
        var sourceCountParams =  CommandParameterAdapter.CreateParametersFromCommand(sourceCountCmd);

        var totalCount = (int)await connection.QuerySingleAsync<long>(
            sourceCountCmd.Sql,
            sourceCountParams,
            commandTimeout: dapperOptions.CommandTimeoutSeconds,
            commandType: CommandType.Text);

        var resultCount = totalCount;
        if (options.GroupBy is { Count: > 0 } || options.Distinct == true)
        {
            resultCount = (int)await connection.QuerySingleAsync<long>(
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql),
                mainParams,
                commandTimeout: dapperOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);
        }

        return (totalCount, resultCount);
    }
}