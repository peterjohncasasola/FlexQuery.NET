using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlAggregateParser
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

            var functionRaw = trimmed[..parenIndex].Trim().ToLowerInvariant();
            if (functionRaw is not ("sum" or "count" or "avg" or "min" or "max"))
                continue;

            var closeParen = trimmed.IndexOf(')', parenIndex);
            if (closeParen < 0) continue;

            var fieldRaw = trimmed[(parenIndex + 1)..closeParen].Trim();
            var remaining = trimmed[(closeParen + 1)..].Trim();

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
                Function = functionRaw,
                Field = field,
                Alias = alias ?? BuildAlias(functionRaw, field)
            });
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
