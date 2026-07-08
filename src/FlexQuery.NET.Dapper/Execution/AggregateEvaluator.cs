using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Execution;

internal static class AggregateEvaluator
{
    public static async Task<Dictionary<string, Dictionary<string, object>>?> GetGrandTotalsAsync(
        DbConnection connection,
        QueryOptions queryOptions,
        SqlTranslator translator,
        DapperQueryOptions options,
        CancellationToken ct)
    {
        var isGrouped = queryOptions.GroupBy is { Count: > 0 };
        if (queryOptions.Aggregates.Count == 0 || isGrouped) return null;

        var aggCommand = translator.TranslateAggregates(queryOptions);
        var aggParameters = CommandParameterAdapter.ToDynamicParameters(aggCommand);

        var aggResult = await connection.QueryFirstOrDefaultAsync(
            aggCommand.Sql, aggParameters,
            commandTimeout: options.CommandTimeoutSeconds, commandType: CommandType.Text);

        if (aggResult is null) return null;

        var grandTotals = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        var rowDict = (IDictionary<string, object>)aggResult;

        foreach (var agg in queryOptions.Aggregates)
        {
            if (!rowDict.TryGetValue(agg.Alias, out var val)) continue;
            
            var fieldName = agg.Field ?? "all";
            
            if (!grandTotals.TryGetValue(fieldName, out var fnDict))
            {
                fnDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                grandTotals[fieldName] = fnDict;
            }
            
            fnDict[agg.Function] = val;
        }

        return grandTotals;
    }
}