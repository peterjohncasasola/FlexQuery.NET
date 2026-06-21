namespace FlexQuery.NET.Parsers.Jql;

public static class SupportedOperators
{
    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        "eq", "neq", "gt", "gte", "lt", "lte",
        "contains", "startswith", "endswith", "like",
        "isnull", "isnotnull", "in", "notin", "between",
        "any", "all", "count"
    };

    public static bool IsSupported(string op) =>
        All.Contains(op, StringComparer.OrdinalIgnoreCase);
}
