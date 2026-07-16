namespace FlexQuery.NET.Parsers;

internal static class IncludeParserHelper
{
    public static List<string> Parse(string? raw, Func<string, Exception> createException)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var segments = SplitTopLevel(raw, ',');
        var result = new List<string>(segments.Count);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
                throw createException($"Invalid include path. The include parameter only supports navigation property paths (e.g. 'orders' or 'orders.items').");

            if (!ParserUtilities.IsValidPropertyPath(trimmed.AsSpan()))
                throw createException($"Invalid include path '{trimmed}'. The include parameter only supports navigation property paths (e.g. 'orders' or 'orders.items').");

            result.Add(trimmed);
        }

        return result;
    }

    public static List<string> SplitTopLevel(string input, char delimiter)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == delimiter && depth == 0)
            {
                parts.Add(input[start..i]);
                start = i + 1;
            }
        }

        parts.Add(input[start..]);
        return parts;
    }
}
