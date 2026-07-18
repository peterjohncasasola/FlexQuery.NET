using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlAggregateParser
{
    public static List<Aggregate> Parse(string? rawSelect)
    {
        if (string.IsNullOrWhiteSpace(rawSelect))
            return [];

        var result = new List<Aggregate>();
        var segments = SplitTopLevel(rawSelect);

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            var trimmed = segment.Trim();
            var parenIndex = trimmed.IndexOf('(');
            if (parenIndex <= 0)
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias]. " +
                    $"Missing opening parenthesis in '{trimmed}'.");
            }

            var functionName = trimmed[..parenIndex].Trim();
            AggregateFunction function;
            try
            {
                function = AggregateFunctionConverter.Parse(functionName);
            }
            catch
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias]. " +
                    $"Unrecognized function '{functionName}' at '{trimmed}'.");
            }

            var closeParen = trimmed.IndexOf(')', parenIndex);
            if (closeParen < 0)
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias]. " +
                    $"Missing closing parenthesis in '{trimmed}'.");
            }

            var fieldRaw = trimmed[(parenIndex + 1)..closeParen].Trim();
            if (fieldRaw.Length == 0)
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias]. " +
                    $"Missing field in '{trimmed}'.");
            }

            if (function == AggregateFunction.Count && fieldRaw == "*")
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"COUNT(*) is not supported. Use COUNT(<collection>) or another aggregate over a property instead.");
            }

            if (function == AggregateFunction.Count && fieldRaw == "*")
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"COUNT(*) is not supported. Use COUNT(<collection>) or another aggregate over a property instead.");
            }

            if (fieldRaw != "*" && !ParserUtilities.IsValidPropertyPath(fieldRaw.AsSpan()))
                throw new FqlParseException(
                    $"Invalid field '{fieldRaw}' in aggregate expression '{rawSelect}'. " +
                    "Field must be a valid property path.");

            string? field;

            if (function == AggregateFunction.Count)
            {
                field = fieldRaw;
            }
            else
            {
                var dotIndex = fieldRaw.LastIndexOf('.');
                field = dotIndex > 0 ? fieldRaw[..dotIndex] : fieldRaw;
            }

            var remaining = trimmed[(closeParen + 1)..].Trim();

            string? alias = null;

            if (remaining.Length >= 2 && remaining.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
            {
                if (remaining.Length == 2)
                {
                    throw new FqlParseException(
                        $"Unable to parse aggregate expression '{rawSelect}'. " +
                        $"Expected format: FUNCTION(Field) [AS Alias]. " +
                        $"Missing alias after AS in '{trimmed}'.");
                }

                var afterAs = remaining[2..];
                if (char.IsWhiteSpace(afterAs[0]))
                {
                    alias = afterAs.Trim();
                    if (alias.Length == 0)
                    {
                        throw new FqlParseException(
                            $"Unable to parse aggregate expression '{rawSelect}'. " +
                            $"Expected format: FUNCTION(Field) [AS Alias]. " +
                            $"Missing alias after AS in '{trimmed}'.");
                    }
                }
            }

            if (string.IsNullOrEmpty(alias) && remaining.Length > 0)
            {
                throw new FqlParseException(
                    $"Unable to parse aggregate expression '{rawSelect}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias]. " +
                    $"Unexpected content after field in '{trimmed}'.");
            }

            if (alias is not null)
            {
                if (!ParserUtilities.IsValidIdentifier(alias.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid alias '{alias}' in aggregate expression. " +
                        "Aliases must be valid identifiers (e.g. 'TotalSales').");
            }

            result.Add(new Aggregate
            {
                Function = function,
                Field = field,
                Alias = alias ?? BuildAlias(function.ToKeyword(), fieldRaw)
            });
        }

        if (result.Count == 0)
        {
            throw new FqlParseException(
                $"Unable to parse aggregate expression '{rawSelect}'. " +
                $"Expected format: FUNCTION(Field) [AS Alias]. No valid aggregate expressions found.");
        }

        return result;
    }

    private static List<string> SplitTopLevel(string input)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                result.Add(input[start..i]);
                start = i + 1;
            }
        }

        result.Add(input[start..]);
        return result;
    }

    private static string BuildAlias(string function, string? field)
    {
        if (field == null)
            return $"{char.ToUpperInvariant(function[0])}{function[1..]}";

        var normalizedField = string.Concat(
            field.Split('.').Select(s =>
                s.Length > 0
                    ? $"{char.ToUpperInvariant(s[0])}{s[1..]}"
                    : string.Empty));

        var funcName = $"{char.ToUpperInvariant(function[0])}{function[1..]}";
        return $"{normalizedField}{funcName}";
    }
}
