using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses SELECT expressions into a list of scalar field paths.
/// Aggregate expressions are no longer parsed from select;
/// they are handled by <see cref="DslAggregateParser"/> instead.
/// </summary>
internal static class SelectParser
{
    /// <summary>
    /// Parses a select string into scalar field paths on options.Select.
    /// Items that look like aggregate function calls (containing parentheses)
    /// are included without path validation.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                $"The 'select' parameter value is empty. Expected comma-separated field paths.");

        var fields = ParserUtilities.SplitCsv(rawSelect);
        var validated = new List<string>(fields.Count);
        foreach (var field in fields)
        {
            // Skip property path validation for items that look like aggregate expressions (contain parentheses)
            if (field.Contains('(') || field.Contains(')'))
            {
                validated.Add(field);
                continue;
            }

            // Extract the property path portion before " AS " for alias expressions
            var pathPart = field;
            var asIndex = field.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex >= 0)
                pathPart = field[..asIndex].Trim();

            if (pathPart.Length > 0 && !ParserUtilities.IsValidPropertyPath(pathPart.AsSpan()))
                throw new DslParseException(
                    $"Invalid property path '{pathPart}' in 'select' parameter. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

            validated.Add(field);
        }

        options.Select = validated;
    }
}