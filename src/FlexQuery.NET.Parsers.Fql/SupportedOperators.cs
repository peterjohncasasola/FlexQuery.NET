namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Provides a centrally defined catalog of all recognized Fql operators
/// and a utility method to test whether a given operator name is supported.
/// </summary>
public static class SupportedOperators
{
    /// <summary>
    /// The complete set of canonical operator names recognized by the Fql parser.
    /// Includes comparison operators (eq, neq, gt, gte, lt, lte),
    /// string matching operators (contains, startswith, endswith, like),
    /// null-check operators (isnull, isnotnull),
    /// set operators (in, notin, between),
    /// and lambda quantifiers (any, all, count).
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        "eq", "neq", "gt", "gte", "lt", "lte",
        "contains", "startswith", "endswith", "like",
        "isnull", "isnotnull", "in", "notin", "between",
        "any", "all", "count"
    };

    /// <summary>
    /// Determines whether the specified operator string is a recognized Fql operator.
    /// Comparison is case-insensitive.
    /// </summary>
    /// <param name="op">The operator name to check (e.g., "eq", "contains").</param>
    /// <returns><see langword="true"/> if the operator is recognized; otherwise <see langword="false"/>.</returns>
    public static bool IsSupported(string op) =>
        All.Contains(op, StringComparer.OrdinalIgnoreCase);
}
