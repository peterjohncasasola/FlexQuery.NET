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

        var op = ParseOperator(rest, rawHaving);
        var value = ParseValue(rest, op, rawHaving);

        var field = fieldRaw.Length == 0 || fieldRaw == "*"
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
            Operator = op,
            Value = value
        };
    }

    private static string ParseOperator(string rest, string rawHaving)
    {
        if (IsWord(rest, "IS NOT NULL"))
            return FilterOperators.IsNotNull;
        if (IsWord(rest, "IS NULL"))
            return FilterOperators.IsNull;
        if (IsWord(rest, "NOT IN"))
            return FilterOperators.NotIn;
        if (IsWord(rest, "BETWEEN"))
            return FilterOperators.Between;
        if (IsWord(rest, "LIKE"))
            return FilterOperators.Like;
        if (IsWord(rest, "IN"))
            return FilterOperators.In;

        if (rest.StartsWith(">=", StringComparison.Ordinal))
            return FilterOperators.GreaterThanOrEq;
        if (rest.StartsWith("<=", StringComparison.Ordinal))
            return FilterOperators.LessThanOrEq;
        
        if (rest.StartsWith("!=", StringComparison.Ordinal) || rest.StartsWith("<>", StringComparison.Ordinal))
            return FilterOperators.NotEqual;

        if (rest.StartsWith(">", StringComparison.Ordinal))
            return FilterOperators.GreaterThan;
        if (rest.StartsWith("<", StringComparison.Ordinal))
            return FilterOperators.LessThan;
        if (rest.StartsWith("=", StringComparison.Ordinal))
            return FilterOperators.Equal;

        throw new FqlParseException(
            $"Unable to parse HAVING expression '{rawHaving}'. " +
            $"Expected format: FUNCTION(Field) OPERATOR value. " +
            $"Unrecognized operator in '{rest}'.");
    }

    private static string? ParseValue(string rest, string op, string rawHaving)
    {
        if (op is FilterOperators.IsNull or FilterOperators.IsNotNull)
        {
            var opStr = op == FilterOperators.IsNull ? "IS NULL" : "IS NOT NULL";
            var afterOp = rest[opStr.Length..].Trim();
            if (afterOp.Length > 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"Unexpected content after '{opStr}'.");
            }

            return null;
        }

        if (op == FilterOperators.Between)
        {
            var afterBetween = rest["BETWEEN".Length..].Trim();
            var andIndex = afterBetween.IndexOf("AND", StringComparison.OrdinalIgnoreCase);
            if (andIndex < 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"BETWEEN requires two values separated by AND.");
            }

            var val1 = afterBetween[..andIndex].Trim().Trim('\'', '"');
            var val2 = afterBetween[(andIndex + 3)..].Trim().Trim('\'', '"');

            if (val1.Length == 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"Missing first value in BETWEEN.");
            }

            if (val2.Length == 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"Missing second value in BETWEEN.");
            }

            return $"{val1},{val2}";
        }

        if (op is FilterOperators.In or FilterOperators.NotIn)
        {
            var notIn = op == FilterOperators.In ? "IN" : "NOT IN";
            var afterIn = rest[notIn.Length..].Trim();

            if (!afterIn.StartsWith("("))
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"IN requires a parenthesized list of values.");
            }

            var closeParen = afterIn.IndexOf(')');
            if (closeParen < 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"Missing closing parenthesis in IN list.");
            }

            var valuesStr = afterIn[1..closeParen].Trim();
            var values = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => v.Trim('\'', '"'))
                .ToArray();

            if (values.Length == 0)
            {
                throw new FqlParseException(
                    $"Unable to parse HAVING expression '{rawHaving}'. " +
                    $"IN list cannot be empty.");
            }

            return string.Join(",", values);
        }

        var opLen = op switch
        {
            FilterOperators.GreaterThanOrEq or FilterOperators.LessThanOrEq or FilterOperators.NotEqual => 2,
            FilterOperators.GreaterThan or FilterOperators.LessThan or FilterOperators.Equal => 1,
            FilterOperators.Like => "LIKE".Length,
            _ => throw new FqlParseException($"Unable to parse HAVING expression '{rawHaving}'. " +
                                             $"Unsupported operator '{op}'.")
        };

        var value = rest[opLen..].Trim().Trim('\'', '"');

        if (string.IsNullOrEmpty(value))
        {
            throw new FqlParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"Missing value after operator.");
        }

        return value;
    }

    private static bool IsWord(string rest, string word)
    {
        if (!rest.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            return false;

        var idx = word.Length;
        if (idx >= rest.Length)
            return true;

        var next = rest[idx];
        return !char.IsLetterOrDigit(next);
    }
}
