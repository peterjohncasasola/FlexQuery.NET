using System.Text.RegularExpressions;
using DynamicQueryable.Security;

namespace DynamicQueryable.Parsers.Jql;

internal static class JqlSafetyValidator
{
    private static readonly Regex AllowedChars = new(
        @"^[A-Za-z0-9_\.\(\)\,\=\!\>\<\-\s'" + "\"" + @"@:/\\]+$",
        RegexOptions.Compiled);

    public static void ValidateSyntax(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new JqlParseException("JQL query expression is empty.");

        if (!AllowedChars.IsMatch(source))
            throw new JqlParseException("JQL query contains invalid characters.");

        var depth = 0;
        foreach (var ch in source)
        {
            if (ch == '(') depth++;
            if (ch == ')') depth--;
            if (depth < 0)
                throw new JqlParseException("JQL query has unbalanced parentheses.");
        }

        if (depth != 0)
            throw new JqlParseException("JQL query has unbalanced parentheses.");
    }

    public static void ValidateField(string field, int position)
    {
        if (!SafePropertyResolver.IsValidPathSyntax(field))
            throw new JqlParseException($"Invalid JQL field '{field}' at position {position}.");
    }
}

