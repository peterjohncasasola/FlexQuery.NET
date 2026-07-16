using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL SELECT expressions into a list of scalar field paths.
/// </summary>
internal static class FqlSelectParser
{
    /// <summary>
    /// Parses an FQL select string into field paths on options.Select.
    /// Accepts property paths and "AS" aliases.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        var fields = ParserUtilities.SplitCsv(rawSelect);
        var selectedFields = new List<string>(fields.Count);
        foreach (var field in fields)
        {
            var asIndex = field.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex >= 0)
            {
                var rawPath = field[..asIndex].Trim();
                var rawAlias = field[(asIndex + 3)..].Trim();

                if (rawPath.Length == 0)
                    throw new FqlParseException(
                        $"Invalid property path in 'select' parameter. " +
                        "Property path must not be empty (e.g. 'Name AS FullName').");

                if (rawAlias.Length == 0)
                    throw new FqlParseException(
                        $"Invalid alias in 'select' parameter. " +
                        "Alias must be a non-empty identifier (e.g. 'Name AS FullName').");

                if (string.Equals(rawAlias, "AS", StringComparison.OrdinalIgnoreCase))
                    throw new FqlParseException(
                        $"Invalid alias in 'select' parameter. " +
                        "The identifier 'AS' is a reserved keyword and cannot be used as an alias.");

                if (!ParserUtilities.IsValidPropertyPath(rawPath.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid property path '{rawPath}' in 'select' parameter. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

                if (!ParserUtilities.IsValidIdentifier(rawAlias.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid alias '{rawAlias}' in 'select' parameter. " +
                        "Aliases must be valid identifiers (e.g. 'FullName').");

                selectedFields.Add($"{rawPath} AS {rawAlias}");
                continue;
            }

            if (field.Contains(':'))
                throw new FqlParseException(
                    $"Invalid alias in 'select' parameter. " +
                    "FQL does not support colon-separated aliases. Use 'AS' syntax instead (e.g. 'Name AS FullName').");

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new FqlParseException(
                    $"Invalid property path '{field}' in 'select' parameter. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

            selectedFields.Add(field);
        }

        options.Select = selectedFields;
    }
}
