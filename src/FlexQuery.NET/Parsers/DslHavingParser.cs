using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses HAVING clause expressions for grouped queries using native DSL syntax.
/// Format: <c>function:field:operator:value</c>
/// </summary>
internal static class DslHavingParser
{
    /// <summary>
    /// Parses a HAVING clause string into a <see cref="HavingCondition"/>.
    /// Throws <see cref="DslParseException"/> if the input is malformed.
    /// </summary>
    public static HavingCondition? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving))
            return null;

        var trimmed = rawHaving.Trim();
        var parts = trimmed.Split(new[] { ':' }, 4);

        if (parts.Length != 4)
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                "Expected format: FUNCTION:Field:OPERATOR:value. " +
                "For example: sum:total:gt:100");

        var fnRaw = parts[0].Trim();
        var field = parts[1].Trim();
        var rawOp = parts[2].Trim();
        var value = parts[3].Trim();

        if (fnRaw.Length == 0)
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                "Expected format: FUNCTION:Field:OPERATOR:value. " +
                "For example: sum:total:gt:100");

        if (value.Length == 0)
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                "Missing value after operator.");

        var aggregateRef = AggregateGrammar.ParseFunctionField($"{fnRaw}:{field}");

        var normalizedOp = FilterOperators.Normalize(rawOp);
        if (!FilterOperators.IsSupported(normalizedOp))
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Unsupported operator '{rawOp}'. " +
                $"Expected one of: eq, neq, gt, gte, lt, lte, contains, startswith, endswith, like, isnull, isnotnull, in, notin, between, any, all, count.");

        return new HavingCondition
        {
            Function = aggregateRef.Function,
            Field = aggregateRef.Field,
            Operator = normalizedOp,
            Value = value
        };
    }
}
