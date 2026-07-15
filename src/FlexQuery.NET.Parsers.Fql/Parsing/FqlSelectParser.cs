using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL SELECT expressions into a list of scalar field paths and expressions.
/// </summary>
internal static class FqlSelectParser
{
    /// <summary>
    /// Parses an FQL select string into field paths on options.Select.
    /// Accepts property paths, "AS" aliases, and expressions with parentheses.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths or expressions.");

        var fields = ParserUtilities.SplitCsv(rawSelect);
        var selectedFields = new List<string>(fields.Count);
        foreach (var field in fields)
        {
            if (field.Contains('(') || field.Contains(')'))
            {
                selectedFields.Add(field);
                continue;
            }

            var asIndex = field.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex >= 0)
            {
                var pathPart = field[..asIndex].Trim();
                if (pathPart.Length > 0 && !ParserUtilities.IsValidPropertyPath(pathPart.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid property path '{pathPart}' in 'select' parameter. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

                selectedFields.Add(field);
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
