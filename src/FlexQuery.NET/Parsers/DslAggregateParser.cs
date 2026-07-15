using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

internal static class DslAggregateParser
{
    public static List<AggregateModel> Parse(string? rawAggregates)
    {
        if (string.IsNullOrWhiteSpace(rawAggregates))
            return [];

        var result = new List<AggregateModel>();
        var items = rawAggregates.Split(',', StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                throw new DslParseException(
                    $"Unable to parse aggregate expression '{rawAggregates}'. Empty aggregate item found.");

            var parts = trimmed.Split(':', StringSplitOptions.TrimEntries);

            if (parts.Length < 2)
                throw new DslParseException(
                    $"Unable to parse aggregate expression '{rawAggregates}'. " +
                    $"Expected format: Function:Field[:Alias]. Invalid item '{trimmed}'.");

            string? aggregateField;
            AggregateFunction function;
            string functionName;

            try
            {
                functionName = parts[0].ToLowerInvariant();
                if (functionName == "average") functionName = "avg";
                function = AggregateFunctionConverter.Parse(functionName);
            }
            catch
            {
                throw new DslParseException(
                    $"Unable to parse aggregate expression '{rawAggregates}'. " +
                    $"Unrecognized aggregate function '{parts[0]}' at '{trimmed}'.");
            }

            var field = parts[1];
            if (field != "*" && !ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new DslParseException(
                    $"Invalid field '{field}' in aggregate expression '{rawAggregates}'. " +
                    "Field must be a valid property path.");

            aggregateField = field == "*" ? null : field;
            string? alias = parts.Length >= 3 ? parts[2] : null;

            result.Add(new AggregateModel
            {
                Function = function,
                Field = aggregateField,
                Alias = alias ?? ParserUtilities.BuildAggregateAlias(functionName, aggregateField)
            });
        }

        if (result.Count == 0)
        {
            throw new DslParseException(
                $"Unable to parse aggregate expression '{rawAggregates}'. " +
                $"Expected format: Function:Field[:Alias]. No valid aggregate expressions found.");
        }

        return result;
    }
}
