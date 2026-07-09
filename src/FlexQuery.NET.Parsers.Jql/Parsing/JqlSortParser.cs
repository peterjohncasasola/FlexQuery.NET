using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlSortParser
{
    public static List<SortNode> Parse(string? sortRaw)
    {
        if (string.IsNullOrWhiteSpace(sortRaw))
            return [];

        var result = new List<SortNode>();
        var items = sortRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0) continue;

            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                var direction = trimmed[(lastSpace + 1)..].Trim();
                var field = trimmed[..lastSpace].Trim();

                if (direction.Equals("DESC", StringComparison.OrdinalIgnoreCase) ||
                    direction.Equals("DESCENDING", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new SortNode { Field = field, Descending = true });
                    continue;
                }

                if (direction.Equals("ASC", StringComparison.OrdinalIgnoreCase) ||
                    direction.Equals("ASCENDING", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new SortNode { Field = field, Descending = false });
                    continue;
                }
            }

            result.Add(new SortNode { Field = trimmed, Descending = false });
        }

        return result;
    }
}
