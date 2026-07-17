using System.Text.RegularExpressions;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses sort expressions including aggregate sorts.
/// Supports string format (field:asc,field:desc).
/// </summary>
internal static class DslSortParser
{
    private static readonly Regex AggregateSortPattern = new(
        @"^(?<collection>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<fn>sum|count|max|min|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a sort string into a list of SortNodes.
    /// Format: field[:asc|:desc] or collection.aggregate(field)[:asc|:desc]
    /// </summary>
    private static List<SortNode> ParseFromString(string? sortRaw)
    {
        if (string.IsNullOrWhiteSpace(sortRaw)) return [];

        var result = new List<SortNode>();
        var span = sortRaw.AsSpan();

        while (!span.IsEmpty)
        {
            var comma = span.IndexOf(',');
            var item = comma < 0 ? span : span[..comma];
            item = item.Trim();

            if (item.IsEmpty)
                throw new DslParseException(
                    $"Unable to parse sort expression '{sortRaw}'. Empty sort item found.");

            ParseSortItem(item, result, sortRaw);

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }

    // Alias for backward compatibility
    public static List<SortNode> Parse(string? sortRaw) => ParseFromString(sortRaw);

    private static void ParseSortItem(ReadOnlySpan<char> item, List<SortNode> result, string rawInput)
    {
        var colon = item.IndexOf(':');
        var fieldSpan = colon < 0 ? item : item[..colon];
        fieldSpan = fieldSpan.Trim();

        if (fieldSpan.IsEmpty)
            throw new DslParseException(
                $"Unable to parse sort expression '{rawInput}'. Empty or invalid field name.");

        var field = fieldSpan.ToString();
        var direction = colon < 0 ? QueryOptionKeys.Asc : item[(colon + 1)..].Trim().ToString();

        if (!direction.Equals(QueryOptionKeys.Asc, StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase))
            throw new DslParseException(
                $"Unable to parse sort expression '{rawInput}'. Invalid sort direction '{direction}' at '{field}'. " +
                $"Expected 'asc' or 'desc'.");

        var isDesc = direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase);

            var aggregateMatch = AggregateSortPattern.Match(field);
            if (aggregateMatch.Success)
            {
                var functionName = aggregateMatch.Groups[QueryOptionKeys.Fn].Value;
                var aggregateFunction = AggregateFunctionConverter.Parse(functionName);
                var collection = aggregateMatch.Groups[QueryOptionKeys.Collection].Value;
                var aggregateField = aggregateMatch.Groups[QueryOptionKeys.Field].Success
                    ? aggregateMatch.Groups[QueryOptionKeys.Field].Value
                    : null;

                if (!ParserUtilities.IsValidPropertyPath(collection.AsSpan()))
                    throw new DslParseException(
                        $"Unable to parse sort expression '{rawInput}'. Invalid collection path '{collection}'.");

                result.Add(new SortNode
                {
                    Field = collection,
                    Descending = isDesc,
                    Aggregate = aggregateFunction,
                    AggregateField = aggregateFunction == AggregateFunction.Count ? null : aggregateField
                });
            }
        else
        {
            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new DslParseException(
                    $"Unable to parse sort expression '{rawInput}'. Invalid field path '{field}'.");

            result.Add(new SortNode
            {
                Field = field,
                Descending = isDesc
            });
        }
    }
}