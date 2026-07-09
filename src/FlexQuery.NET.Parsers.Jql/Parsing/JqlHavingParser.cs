using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlHavingParser
{
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sum", "count", "avg", "min", "max"
    };

    public static HavingCondition? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving))
            return null;

        var trimmed = rawHaving.Trim();

        var parenIndex = trimmed.IndexOf('(');
        if (parenIndex <= 0)
            return null;

        var function = trimmed[..parenIndex].Trim().ToLowerInvariant();
        if (!AllowedFunctions.Contains(function))
            return null;

        var closeParen = trimmed.IndexOf(')', parenIndex);
        if (closeParen < 0)
            return null;

        var fieldRaw = trimmed[(parenIndex + 1)..closeParen].Trim();
        var rest = trimmed[(closeParen + 1)..].Trim();

        if (rest.Length == 0)
            return null;

        var opStart = 0;
        string op;
        string value;

        if (rest.StartsWith(">="))
        {
            op = ">=";
            opStart = 2;
        }
        else if (rest.StartsWith("<="))
        {
            op = "<=";
            opStart = 2;
        }
        else if (rest.StartsWith("!="))
        {
            op = "!=";
            opStart = 2;
        }
        else if (rest.StartsWith(">"))
        {
            op = ">";
            opStart = 1;
        }
        else if (rest.StartsWith("<"))
        {
            op = "<";
            opStart = 1;
        }
        else if (rest.StartsWith("="))
        {
            op = "=";
            opStart = 1;
        }
        else
        {
            return null;
        }

        value = rest[opStart..].Trim().Trim('\'', '"');

        if (value.Length == 0)
            return null;

        string? field = fieldRaw.Length == 0 || fieldRaw == "*"
            ? null
            : fieldRaw;

        return new HavingCondition
        {
            Function = function,
            Field = field,
            Operator = FilterOperators.Normalize(op),
            Value = value
        };
    }
}
