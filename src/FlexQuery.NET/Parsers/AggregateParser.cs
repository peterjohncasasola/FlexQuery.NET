using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers;

internal static class AggregateParser
{
    public static List<AggregateModel> Parse(string? rawAggregates)
    {
        if (string.IsNullOrWhiteSpace(rawAggregates))
            return [];

        var result = new List<AggregateModel>();
        var items = rawAggregates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0) continue;

            var parts = trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Format: Field:Function[:Alias]
            if (parts.Length < 2) continue;

            var field = parts[0];
            AggregateFunction function;
            string functionName;

            try
            {
                functionName = parts[1].ToLowerInvariant();
                if (functionName == "average") functionName = "avg";
                function = AggregateFunctionConverter.Parse(functionName);
            }
            catch
            {
                continue;
            }

            string? alias = parts.Length >= 3 ? parts[2] : null;

            string? aggregateField = field == "*" ? null : field;

            result.Add(new AggregateModel
            {
                Function = function,
                Field = aggregateField,
                Alias = alias ?? ParserUtilities.BuildAggregateAlias(functionName, aggregateField)
            });
        }

        return result;
    }
}
