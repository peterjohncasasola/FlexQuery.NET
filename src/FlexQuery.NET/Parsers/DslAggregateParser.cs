using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

internal static class DslAggregateParser
{
    public static List<Aggregate> Parse(string? rawAggregates)
    {
        if (string.IsNullOrWhiteSpace(rawAggregates))
            return [];

        var result = new List<Aggregate>();
        var items = rawAggregates.Split(',', StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                throw new DslParseException(
                    "Unable to parse aggregate expression. Empty aggregate item found.",
                    position: -1);

            var parts = trimmed.Split(':');

            if (parts.Length < 2)
                throw new DslParseException(
                    "Unable to parse aggregate expression. Expected format: Function:Field[:Alias].",
                    position: -1);

            for (var i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            if (parts[0].Length == 0)
                throw new DslParseException(
                    "Unable to parse aggregate expression. Expected format: Function:Field[:Alias]. Missing function.",
                    position: -1);

            if (parts[1].Length == 0)
                throw new DslParseException(
                    "Unable to parse aggregate expression. Expected format: Function:Field[:Alias]. Missing field.",
                    position: -1);

            if (parts.Length > 3)
                throw new DslParseException(
                    "Unable to parse aggregate expression. Expected format: Function:Field[:Alias]. Too many parts.",
                    position: -1);

            var aggregateRef = AggregateGrammar.ParseFunctionField($"{parts[0]}:{parts[1]}");

            if (parts.Length == 3)
            {
                var aliasPart = parts[2];
                if (string.IsNullOrWhiteSpace(aliasPart))
                    throw new DslParseException(
                        "Unable to parse aggregate expression. Expected format: Function:Field[:Alias]. Empty alias.",
                        position: -1);
                
                if (!ParserUtilities.IsValidIdentifier(aliasPart.AsSpan()))
                    throw new DslParseException(
                        $"Invalid alias '{aliasPart}' in aggregate expression. " +
                        "Aliases must be valid identifiers (e.g. 'TotalSales').",
                        position: -1);

                result.Add(new Aggregate
                {
                    Function = aggregateRef.Function,
                    Field = aggregateRef.Field,
                    Alias = aliasPart
                });
            }
            else
            {
                result.Add(new Aggregate
                {
                    Function = aggregateRef.Function,
                    Field = aggregateRef.Field,
                    Alias = ParserUtilities.BuildAggregateAlias(aggregateRef.FunctionName, aggregateRef.Field)
                });
            }
        }

        if (result.Count == 0)
        {
            throw new DslParseException(
                "Unable to parse aggregate expression. Expected format: Function:Field[:Alias]. No valid aggregate expressions found.",
                position: -1);
        }

        return result;
    }
}
