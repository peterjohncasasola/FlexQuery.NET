using System.Text.RegularExpressions;

namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlSafetyValidator
{
    private static readonly Regex AllowedChars = new(
        @"^[A-Za-z0-9_\.\(\)\[\]\,\=\!\.\>\<\-\s'" + "\"" + @"@:/\\%]+$",
        RegexOptions.Compiled);

    private static readonly Regex SegmentPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void ValidateSyntax(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new JqlParseException("JQL query expression is empty.");

        if (!AllowedChars.IsMatch(source))
            throw new JqlParseException("JQL query contains invalid characters.");

        var depth = 0;
        var bracketDepth = 0;
        foreach (var ch in source)
        {
            if (ch == '(') depth++;
            if (ch == ')') depth--;
            if (ch == '[') bracketDepth++;
            if (ch == ']') bracketDepth--;
            if (depth < 0)
                throw new JqlParseException("JQL query has unbalanced parentheses.");
            if (bracketDepth < 0)
                throw new JqlParseException("JQL query has unbalanced square brackets.");
        }

        if (depth != 0)
            throw new JqlParseException("JQL query has unbalanced parentheses.");
        if (bracketDepth != 0)
            throw new JqlParseException("JQL query has unbalanced square brackets.");
    }

    public static void ValidateField(string field, int position)
    {
        if (!IsValidPathSyntax(field))
            throw new JqlParseException($"Invalid JQL field '{field}' at position {position}.");
    }

    private static bool IsValidPathSyntax(string? fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath)) return false;

        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return false;

        return segments.All(s => SegmentPattern.IsMatch(s));
    }
}
