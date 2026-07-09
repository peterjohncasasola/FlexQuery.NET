namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlGroupByParser
{
    public static List<string> Parse(string? groupByRaw)
    {
        if (string.IsNullOrWhiteSpace(groupByRaw))
            return [];

        return groupByRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
    }
}
