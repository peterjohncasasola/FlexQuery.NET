using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Adapters.Kendo.Mapping;

/// <summary>
/// Maps Kendo UI filter operators to FlexQuery.NET canonical filter operators.
/// </summary>
internal static class KendoOperatorMapper
{
    private static readonly Dictionary<string, string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = FilterOperators.Equal,
        ["neq"] = FilterOperators.NotEqual,
        ["contains"] = FilterOperators.Contains,
        ["startswith"] = FilterOperators.StartsWith,
        ["endswith"] = FilterOperators.EndsWith,
        ["gt"] = FilterOperators.GreaterThan,
        ["gte"] = FilterOperators.GreaterThanOrEq,
        ["lt"] = FilterOperators.LessThan,
        ["lte"] = FilterOperators.LessThanOrEq,
        ["isnull"] = FilterOperators.IsNull,
        ["isnotnull"] = FilterOperators.IsNotNull,
        ["isempty"] = FilterOperators.IsNull,
        ["isnotempty"] = FilterOperators.IsNotNull
    };

    /// <summary>
    /// Maps a Kendo UI filter operator to its corresponding FlexQuery.NET canonical operator.
    /// </summary>
    /// <param name="kendoOperator">The Kendo UI operator string (e.g., "eq", "neq", "contains").</param>
    /// <returns>The canonical FlexQuery.NET operator string.</returns>
    /// <exception cref="FormatException">Thrown when the operator is not supported.</exception>
    public static string Map(string? kendoOperator)
    {
        if (string.IsNullOrWhiteSpace(kendoOperator))
        {
            throw new FormatException("Kendo filter operator is missing.");
        }

        var normalizedOperator = NormalizeKey(kendoOperator);

        if (Operators.TryGetValue(normalizedOperator, out var mappedOperator))
        {
            return mappedOperator;
        }

        throw new FormatException($"Unsupported Kendo filter operator '{kendoOperator}'.");
    }

    /// <summary>
    /// Normalizes an operator key to lowercase with spaces removed.
    /// </summary>
    /// <param name="value">The operator string to normalize.</param>
    /// <returns>The normalized operator string.</returns>
    private static string NormalizeKey(string value)
    {
        return value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}
