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
        QueryOptions options,
        SqlTranslator translator,
        DapperQueryOptions dapperOptions,
        CancellationToken ct)
    {
        if (options.Aggregates.Count == 0 || (options.GroupBy?.Count > 0))
            return null;

        ct.ThrowIfCancellationRequested();

        var aggCommand = translator.TranslateAggregates(options);
        var aggParams =  CommandParameterAdapter.CreateParametersFromCommand(aggCommand);

        var aggResult = await connection.QueryFirstOrDefaultAsync(
            aggCommand.Sql,
            aggParams,
            commandTimeout: dapperOptions.CommandTimeoutSeconds,
            commandType: CommandType.Text);

        if (aggResult is null) return null;

        var grandTotals = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        var rowDict = (IDictionary<string, object>)aggResult;

        foreach (var agg in options.Aggregates)
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