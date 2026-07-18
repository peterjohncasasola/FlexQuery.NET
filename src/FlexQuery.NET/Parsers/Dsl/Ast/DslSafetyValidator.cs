using System.Text.RegularExpressions;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Parsers.Dsl;

internal static class DslSafetyValidator
{
    private static readonly Regex AllowedChars = new(
        @"^[A-Za-z0-9_\.\:\&\|\(\)\,\-'" + "\"" + @"\\\s@\/\?\=\%\+\#\~!]+$",
        RegexOptions.Compiled);

    private static readonly Regex FieldPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void ValidateSyntax(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new DslParseException("DSL filter expression is empty.", position: -1);

        if (!AllowedChars.IsMatch(source))
            throw new DslParseException("DSL filter contains invalid characters.", position: -1);

        var depth = 0;
        foreach (var ch in source)
        {
            if (ch == '(') depth++;
            if (ch == ')') depth--;
            if (depth < 0)
                throw new DslParseException("DSL filter has unbalanced parentheses.", position: -1);
        }

        if (depth != 0)
            throw new DslParseException("DSL filter has unbalanced parentheses.", position: -1);
    }

    public static void ValidateFieldToken(string field, int position)
    {
        if (!FieldPattern.IsMatch(field))
            throw new DslParseException($"Invalid DSL field '{field}'.", position: position, found: field);
    }

    public static void ValidateOperatorToken(string op, int position)
    {
        var normalized = FilterOperators.Normalize(op);
        if (!FilterOperators.IsSupported(normalized))
            throw new DslParseException($"Unsupported DSL operator '{op}'.", position: position, found: op);
    }
}
