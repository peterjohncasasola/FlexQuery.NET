using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers;

internal static class AggregateParser
{
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sum", "count", "avg", "average", "min", "max"
    };

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
            var function = parts[1].ToLowerInvariant();
            if (function == "average") function = "avg";

            if (!AllowedFunctions.Contains(function)) continue;

            string? alias = parts.Length >= 3 ? parts[2] : null;

            string? aggregateField = field == "*" ? null : field;

            result.Add(new AggregateModel
            {
                Function = function,
                Field = aggregateField,
                Alias = alias ?? ParserUtilities.BuildAggregateAlias(function, aggregateField)
            });
        }

        return result;
    }
}
