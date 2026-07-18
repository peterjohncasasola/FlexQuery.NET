using FlexQuery.NET.Constants;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses sort expressions including aggregate sorts.
/// Supports string format (field:asc,field:desc) and aggregate format (function:target:direction).
/// </summary>
internal static class DslSortParser
{
    
    /// <summary>
    /// Parses a sort string into a list of SortNodes.
    /// Format: field[:direction] or function:target[:direction]
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
                    "Unable to parse sort expression. Empty sort item found.",
                    position: -1);

            ParseSortItem(item, result, sortRaw);

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }

    public static List<SortNode> Parse(string? sortRaw) => ParseFromString(sortRaw);

    /// <summary>
    /// Determines whether the sort item is a normal field sort or an aggregate sort
    /// by inspecting the first colon-separated segment.
    /// </summary>
    private static void ParseSortItem(ReadOnlySpan<char> item, List<SortNode> result, string rawInput)
    {
        var firstColon = item.IndexOf(':');

        if (firstColon < 0)
        {
            ParseFieldSort(item, result, rawInput);
            return;
        }

        var firstSegment = item[..firstColon].Trim();
        var remaining = item[(firstColon + 1)..];

        if (firstSegment.IsEmpty)
            throw new DslParseException(
                "Unable to parse sort expression. Empty sort item found.",
                position: -1);

        if (AggregateFunctionHelper.IsSupported(firstSegment.ToString()))
        {
            ParseAggregateSort(firstSegment, remaining, result, rawInput);
            return;
        }

        ParseFieldSort(item, result, rawInput);
    }

    /// <summary>
    /// Parses a simple field sort in the format field[:direction].
    /// </summary>
    private static void ParseFieldSort(ReadOnlySpan<char> item, List<SortNode> result, string rawInput)
    {
        var colon = item.IndexOf(':');
        var fieldSpan = colon < 0 ? item : item[..colon];
        fieldSpan = fieldSpan.Trim();

        if (fieldSpan.IsEmpty)
            throw new DslParseException(
                "Unable to parse sort expression. Empty or invalid field name.",
                position: -1);

        var field = fieldSpan.ToString();
        var direction = colon < 0 ? QueryOptionKeys.Asc : item[(colon + 1)..].Trim().ToString();

        if (!direction.Equals(QueryOptionKeys.Asc, StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase))
            throw new DslParseException(
                "Unable to parse sort expression. Invalid sort direction. Expected 'asc' or 'desc'.",
                position: -1);

        if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
            throw new DslParseException(
                $"Unable to parse sort expression. Invalid field path '{field}'.",
                position: -1);

        var isDesc = direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase);

        result.Add(new SortNode
        {
            Field = field,
            Descending = isDesc
        });
    }

    /// <summary>
    /// Parses an aggregate sort in the format function:target[:direction].
    /// For sum/avg/min/max, the target is split on the last dot into Field and AggregateField.
    /// For count, the entire target becomes Field and AggregateField is always null.
    /// </summary>
    private static void ParseAggregateSort(
        ReadOnlySpan<char> functionSpan,
        ReadOnlySpan<char> remaining,
        List<SortNode> result,
        string rawInput)
    {
        var functionName = functionSpan.ToString();
        var secondColon = remaining.IndexOf(':');

        ReadOnlySpan<char> targetSpan;
        ReadOnlySpan<char> directionSpan;

        if (secondColon >= 0)
        {
            targetSpan = remaining[..secondColon].Trim();
            directionSpan = remaining[(secondColon + 1)..].Trim();
        }
        else
        {
            targetSpan = remaining.Trim();
            directionSpan = [];
        }

        if (targetSpan.IsEmpty)
            throw new DslParseException(
                "Unable to parse sort expression. Missing aggregate target.",
                position: -1);

        var target = targetSpan.ToString();

        if (!ParserUtilities.IsValidPropertyPath(target.AsSpan()))
            throw new DslParseException(
                $"Unable to parse sort expression. Invalid target '{target}' in aggregate sort.",
                position: -1);

        AggregateFunction aggregateFunction;
        try
        {
            aggregateFunction = AggregateFunctionConverter.Parse(functionName);
        }
        catch
        {
            throw new DslParseException(
                $"Unable to parse sort expression. Unrecognized aggregate function '{functionName}'. " +
                $"Expected one of: sum, count, avg, min, max.",
                position: -1);
        }

        var direction = directionSpan.IsEmpty ? QueryOptionKeys.Asc : directionSpan.ToString();

        if (!direction.Equals(QueryOptionKeys.Asc, StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase))
            throw new DslParseException(
                "Unable to parse sort expression. Invalid sort direction. Expected 'asc' or 'desc'.",
                position: -1);

        var isDesc = direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase);

        string field;
        string? aggregateField;

        if (aggregateFunction == AggregateFunction.Count)
        {
            field = target;
            aggregateField = null;
        }
        else
        {
            var lastDot = target.LastIndexOf('.');
            if (lastDot >= 0)
            {
                field = target[..lastDot];
                aggregateField = target[(lastDot + 1)..];
            }
            else
            {
                field = target;
                aggregateField = null;
            }
        }

        result.Add(new SortNode
        {
            Field = field,
            Descending = isDesc,
            Aggregate = aggregateFunction,
            AggregateField = aggregateField
        });
    }
}
