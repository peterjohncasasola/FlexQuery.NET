using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Execution;

internal static class CountEvaluator
{
    public static async Task<(int? totalCount, int? resultCount)> GetCountsAsync(
        DbConnection connection,
        QueryOptions queryOptions,
        SqlTranslator translator,
        SqlCommand mainCommand,
        DynamicParameters? mainParams,
        DapperQueryOptions options)
    {
        
        var shouldIncludeCount = options.IncludeTotalCount && (queryOptions.IncludeCount ?? true);
        if (!shouldIncludeCount) return (null, null);

        var sourceCountCommand = translator.TranslateSourceCount(queryOptions);
        var sourceCountParameters = CommandParameterAdapter.ToDynamicParameters(sourceCountCommand);

        var totalCount = (int)await connection.QuerySingleAsync<long>(
            sourceCountCommand.Sql, sourceCountParameters,
            commandTimeout: options.CommandTimeout, commandType: CommandType.Text);

        var resultCount = queryOptions.GroupBy is { Count: > 0 } || queryOptions.Distinct == true
            ? (int)await connection.QuerySingleAsync<long>(
                SqlCountBuilder.ExtractCountSql(mainCommand.Sql), mainParams!,
                commandTimeout: options.CommandTimeout, commandType: CommandType.Text)
            : totalCount;

        return (totalCount, resultCount);
    }
}
