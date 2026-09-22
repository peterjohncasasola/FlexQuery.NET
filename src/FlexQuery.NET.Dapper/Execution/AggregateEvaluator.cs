using System.Data;
using Dapper;
using FlexQuery.NET.Dapper.Context;
using FlexQuery.NET.Dapper.Diagnostics;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Dapper.Execution;

internal static class AggregateEvaluator
{
    public static async Task<Dictionary<string, Dictionary<string, object>>?> GetGrandTotalsAsync(
        DapperQueryContext queryContext
        ,Func<string, string>? publicFieldNameResolver = null)
    {
        var queryOptions = queryContext.QueryOptions;
        var translator = queryContext.SqlTranslator;
        var sqlLogger = queryContext.SqlLogger;
        var ct = queryContext.CancellationToken;
        var connection = queryContext.Connection;
        var options = queryContext.DapperQueryOptions;
        
        var isGrouped = queryOptions.GroupBy is { Count: > 0 };
        if (queryOptions.Aggregates.Count == 0 || isGrouped) return null;

        var aggCommand = translator.TranslateAggregates(queryOptions);
        var aggParameters = CommandParameterAdapter.ToDynamicParameters(aggCommand);

        DapperSqlLog.Command(sqlLogger, aggCommand.Sql, aggCommand.Parameters);

        var commandDefinition = new CommandDefinition(
            aggCommand.Sql, 
            aggParameters,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text,
            cancellationToken: ct);

        var aggResult = await connection.QueryFirstOrDefaultAsync(commandDefinition);

        if (aggResult is null) return null;

        var grandTotals = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        var rowDict = (IDictionary<string, object>)aggResult;

        foreach (var agg in queryOptions.Aggregates)
        {
            if (!rowDict.TryGetValue(agg.Alias, out var val)) continue;

            var fieldName = agg.Field ?? "all";
            if (publicFieldNameResolver is not null)
                fieldName = publicFieldNameResolver(fieldName);

            if (!grandTotals.TryGetValue(fieldName, out var fnDict))
            {
                fnDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                grandTotals[fieldName] = fnDict;
            }

            fnDict[agg.Function.ToKeyword()] = val;
        }

        return grandTotals;
    }
}