using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlAggregateParser
{
    public static List<AggregateModel> Parse(string? rawSelect)
    {
        if (string.IsNullOrWhiteSpace(rawSelect))
            return [];

        var result = new List<AggregateModel>();
        var segments = SplitTopLevel(rawSelect);

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            var trimmed = segment.Trim();
            var parenIndex = trimmed.IndexOf('(');
            if (parenIndex <= 0) continue;

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
                    $"Missing closing parenthesis in '{trimmed}'. " +
                    $"Expected format: FUNCTION(Field) [AS Alias].");
            }

            var fieldRaw = trimmed[(parenIndex + 1)..closeParen].Trim();
            var remaining = trimmed[(closeParen + 1)..].Trim();

            if (fieldRaw.Length > 0 && fieldRaw != "*" && !ParserUtilities.IsValidPropertyPath(fieldRaw.AsSpan()))
                throw new FqlParseException(
                    $"Invalid field '{fieldRaw}' in aggregate expression '{rawSelect}'. " +
                    "Field must be a valid property path.");

            string? field = fieldRaw.Length == 0 || fieldRaw == "*"
                ? null
                : fieldRaw;

            string? alias = null;
            if (remaining.StartsWith("AS ", StringComparison.OrdinalIgnoreCase))
            {
                alias = remaining[3..].Trim();
                if (alias.Length == 0) alias = null;
            }

            result.Add(new AggregateModel
            {
                Function = function,
                Field = field,
                Alias = alias ?? BuildAlias(AggregateFunctionConverter.ToKeyword(function), field)
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

    internal static string BuildAlias(string function, string? field)
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
