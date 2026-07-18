using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Parsers;

internal static class GroupByParser
{
    public static List<string> Parse(string? groupByRaw)
    {
        if (string.IsNullOrWhiteSpace(groupByRaw))
            return [];

        var result = new List<string>();
        var items = groupByRaw.Split(',', StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                throw new FlexQueryParseException(
                    "Unable to parse groupBy expression. Empty group item found.",
                    position: -1);

            if (!ParserUtilities.IsValidPropertyPath(trimmed.AsSpan()))
                throw new FlexQueryParseException(
                    $"Invalid property path '{trimmed}' in groupBy expression. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Category' or 'Customer.Region').",
                    position: -1);

            result.Add(trimmed);
        }

        if (result.Count == 0)
        {
            throw new FlexQueryParseException(
                "Unable to parse groupBy expression. Expected comma-separated field paths.",
                position: -1);
        }

        return result;
    }
}