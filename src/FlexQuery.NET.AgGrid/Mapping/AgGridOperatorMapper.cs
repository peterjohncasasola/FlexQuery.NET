using FlexQuery.NET.Constants;

namespace FlexQuery.NET.AgGrid.Mapping;

public static class AgGridOperatorMapper
{
    private static readonly Dictionary<string, string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text:contains"] = FilterOperators.Contains,
        ["text:equals"] = FilterOperators.Equal,
        ["text:notEqual"] = FilterOperators.NotEqual,
        ["text:startsWith"] = FilterOperators.StartsWith,
        ["text:endsWith"] = FilterOperators.EndsWith,
        ["text:inRange"] = FilterOperators.Between,
        ["text:blank"] = FilterOperators.IsNull,
        ["text:notBlank"] = FilterOperators.IsNotNull,

        ["number:equals"] = FilterOperators.Equal,
        ["number:notEqual"] = FilterOperators.NotEqual,
        ["number:greaterThan"] = FilterOperators.GreaterThan,
        ["number:greaterThanOrEqual"] = FilterOperators.GreaterThanOrEq,
        ["number:lessThan"] = FilterOperators.LessThan,
        ["number:lessThanOrEqual"] = FilterOperators.LessThanOrEq,
        ["number:inRange"] = FilterOperators.Between,
        ["number:blank"] = FilterOperators.IsNull,
        ["number:notBlank"] = FilterOperators.IsNotNull,

        ["date:equals"] = FilterOperators.Equal,
        ["date:notEqual"] = FilterOperators.NotEqual,
        ["date:greaterThan"] = FilterOperators.GreaterThan,
        ["date:greaterThanOrEqual"] = FilterOperators.GreaterThanOrEq,
        ["date:lessThan"] = FilterOperators.LessThan,
        ["date:lessThanOrEqual"] = FilterOperators.LessThanOrEq,
        ["date:after"] = FilterOperators.GreaterThan,
        ["date:afterOrEqual"] = FilterOperators.GreaterThanOrEq,
        ["date:before"] = FilterOperators.LessThan,
        ["date:beforeOrEqual"] = FilterOperators.LessThanOrEq,
        ["date:inRange"] = FilterOperators.Between,
        ["date:blank"] = FilterOperators.IsNull,
        ["date:notBlank"] = FilterOperators.IsNotNull
    };

    public static string Map(string filterType, string? agGridOperator)
    {
        if (string.IsNullOrWhiteSpace(agGridOperator))
        {
            throw new FormatException("AG Grid filter operator is missing.");
        }

        var normalizedOperator = NormalizeKey(agGridOperator);

        if (Operators.TryGetValue($"{filterType}:{normalizedOperator}", out var mappedOperator))
        {
            return mappedOperator;
        }

        throw new FormatException($"Unsupported AG Grid filter operator '{agGridOperator}' for filter type '{filterType}'.");
    }

    private static string NormalizeKey(string value)
    {
        return value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}
