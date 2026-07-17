using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlHavingParser
{
    public static HavingCondition? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving))
            return null;

        var trimmed = rawHaving.Trim();

        var parenIndex = trimmed.IndexOf('(');
        if (parenIndex <= 0)
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Expected format: FUNCTION(Field) OPERATOR value. " +
                $"Missing function call.");

        AggregateFunction function;
        try
        {
            function = AggregateFunctionConverter.Parse(trimmed[..parenIndex].Trim());
        }
        catch
        {
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Expected format: FUNCTION(Field) OPERATOR value. " +
                $"Unrecognized aggregate function.");
        }

        var closeParen = trimmed.IndexOf(')', parenIndex);
        if (closeParen < 0)
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Missing closing parenthesis.");

        var fieldRaw = trimmed[(parenIndex + 1)..closeParen].Trim();
        var rest = trimmed[(closeParen + 1)..].Trim();

        if (rest.Length == 0)
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Expected format: FUNCTION(Field) OPERATOR value. " +
                $"Missing operator and value after function call.");

        if (function == AggregateFunction.Count && fieldRaw == "*")
        {
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"COUNT(*) is not supported. Use COUNT(<collection>) or another aggregate over a property instead.");
        }

        var opStart = 0;
        string op;
        string value;

        if (rest.StartsWith(">=")) { op = ">="; opStart = 2; }
        else if (rest.StartsWith("<=")) { op = "<="; opStart = 2; }
        else if (rest.StartsWith("!=")) { op = "!="; opStart = 2; }
        else if (rest.StartsWith(">")) { op = ">"; opStart = 1; }
        else if (rest.StartsWith("<")) { op = "<"; opStart = 1; }
        else if (rest.StartsWith("=")) { op = "="; opStart = 1; }
        else
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Expected format: FUNCTION(Field) OPERATOR value. " +
                $"Unrecognized operator in '{rest}'.");

        value = rest[opStart..].Trim().Trim('\'', '"');

        if (value.Length == 0)
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Missing value after operator.");

        string? field = fieldRaw.Length == 0 || fieldRaw == "*"
            ? null
            : fieldRaw;

        if (field is not null && !ParserUtilities.IsValidPropertyPath(field.AsSpan()))
            throw new FqlParseException(
                $"Invalid field '{field}' in HAVING expression '{rawHaving}'. " +
                "Field must be a valid property path.");

        return new HavingCondition
        {
            Function = function,
            Field = field,
            Operator = FilterOperators.Normalize(op),
            Value = value
        };
    }
}
