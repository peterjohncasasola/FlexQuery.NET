using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>
/// Parses DSL SELECT expressions into a list of scalar field paths.
/// </summary>
internal static class DslSelectParser
{
    /// <summary>
    /// Parses a DSL select string into scalar field paths on options.Select.
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
            var colonIndex = field.IndexOf(':');
            if (colonIndex >= 0)
            {
                if (field.IndexOf(':', colonIndex + 1) >= 0)
                    throw new DslParseException(
                        $"Invalid alias in 'select' parameter. " +
                        "Colon-separated aliases must contain exactly one colon (e.g. 'Name:FullName').");

                var rawPath = field[..colonIndex].Trim();
                var rawAlias = field[(colonIndex + 1)..].Trim();

                if (rawPath.Length == 0)
                    throw new DslParseException(
                        $"Invalid property path in 'select' parameter. " +
                        "Property path must not be empty (e.g. 'Name:FullName').");

                if (rawAlias.Length == 0)
                    throw new DslParseException(
                        $"Invalid alias in 'select' parameter. " +
                        "Alias must be a non-empty identifier (e.g. 'Name:FullName').");

                if (string.Equals(rawAlias, "AS", StringComparison.OrdinalIgnoreCase))
                    throw new DslParseException(
                        $"Invalid alias in 'select' parameter. " +
                        "The identifier 'AS' is a reserved keyword and cannot be used as an alias.");

                if (!ParserUtilities.IsValidPropertyPath(rawPath.AsSpan()))
                    throw new DslParseException(
                        $"Invalid property path '{rawPath}' in 'select' parameter. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

                if (!ParserUtilities.IsValidIdentifier(rawAlias.AsSpan()))
                    throw new DslParseException(
                        $"Invalid alias '{rawAlias}' in 'select' parameter. " +
                        "Aliases must be valid identifiers (e.g. 'FullName').");

                validated.Add($"{rawPath} AS {rawAlias}");
                continue;
            }

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new DslParseException(
                    $"Invalid property path '{field}' in 'select' parameter. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

            validated.Add(field);
        }

        options.Select = validated;
    }
}
