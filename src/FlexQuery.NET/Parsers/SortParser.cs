using System.Text.RegularExpressions;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses sort expressions including aggregate sorts.
/// Supports string format (field:asc,field:desc).
/// </summary>
internal static class SortParser
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

            if (!item.IsEmpty)
            {
                ParseSortItem(item, result);
            }

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }

    // Alias for backward compatibility
    public static List<SortNode> Parse(string? sortRaw) => ParseFromString(sortRaw);

    private static void ParseSortItem(ReadOnlySpan<char> item, List<SortNode> result)
    {
        var colon = item.IndexOf(':');
        var fieldSpan = colon < 0 ? item : item[..colon];
        fieldSpan = fieldSpan.Trim();

        if (fieldSpan.IsEmpty) return;

        var field = fieldSpan.ToString();
        var direction = colon < 0 ? QueryOptionKeys.Asc : item[(colon + 1)..].Trim().ToString();
        var isDesc = direction.Equals(QueryOptionKeys.Desc, StringComparison.OrdinalIgnoreCase);

        var aggregateMatch = AggregateSortPattern.Match(field);
        if (aggregateMatch.Success)
        {
            var aggregate = aggregateMatch.Groups[QueryOptionKeys.Fn].Value.ToLowerInvariant();
            var collection = aggregateMatch.Groups[QueryOptionKeys.Collection].Value;
            var aggregateField = aggregateMatch.Groups[QueryOptionKeys.Field].Success 
                ? aggregateMatch.Groups[QueryOptionKeys.Field].Value 
                : null;

            result.Add(new SortNode
            {
                Field = collection,
                Descending = isDesc,
                Aggregate = aggregate,
                AggregateField = string.IsNullOrWhiteSpace(aggregateField) ? null : aggregateField
            });
        }
        else
        {
            result.Add(new SortNode
            {
                Field = field,
                Descending = isDesc
            });
        }
    }
}