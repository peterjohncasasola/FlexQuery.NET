using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>
/// Parses DSL SELECT expressions into a list of scalar field paths or a SelectionNode tree.
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

        if (rawSelect is not null && (rawSelect.Contains('(') || rawSelect.Contains('*')))
        {
            options.SelectTree = ParseToSelectionTree(rawSelect);
            options.Select = null;
            return;
        }

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

    /// <summary>
    /// Parses a DSL select string into a <see cref="SelectionNode"/> tree.
    /// Supports recursive nested selection syntax such as <c>Customer(Id,Name)</c>.
    /// </summary>
    /// <param name="rawSelect">The raw select string.</param>
    /// <returns>A <see cref="SelectionNode"/> representing the selection tree.</returns>
    /// <exception cref="DslParseException">Thrown when the value contains invalid nested syntax.</exception>
    public static SelectionNode? ParseToSelectionTree(string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        var root = new SelectionNode();
        var span = rawSelect.AsSpan();

        ParseSelectionList(span, root, isRoot: true);

        return root;
    }

    private static void ParseSelectionList(ReadOnlySpan<char> span, SelectionNode parent, bool isRoot)
    {
        int i = 0;
        bool hasSelection = false;
        bool wildcardUsed = false;

        while (i < span.Length)
        {
            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i >= span.Length)
                break;

            if (span[i] == ',')
            {
                if (!hasSelection)
                    throw new DslParseException(
                        "Empty field in 'select' parameter. Expected identifier before comma.");

                i++;

                int j = i;
                while (j < span.Length && char.IsWhiteSpace(span[j]))
                    j++;

                if (j >= span.Length)
                    throw new DslParseException(
                        "Empty field in 'select' parameter. Trailing comma is not allowed.");

                if (span[j] == ',')
                    throw new DslParseException(
                        "Empty field in 'select' parameter. Expected identifier between commas.");

                continue;
            }

            if (isRoot && span[i] == ')')
                throw new DslParseException(
                    "Unexpected closing parenthesis in 'select' parameter.");

            string identifier;
            if (span[i] == '*')
            {
                i++;
                identifier = "*";
            }
            else
            {
                int start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                    i++;

                if (i == start)
                    throw new DslParseException(
                        "Empty field in 'select' parameter. Expected identifier or '*'.");

                identifier = span.Slice(start, i - start).ToString();
            }

            if (identifier == "*")
            {
                if (!isRoot)
                    throw new DslParseException(
                        "Wildcard selection is only supported at the root level.");

                if (hasSelection)
                    throw new DslParseException(
                        "Root wildcard cannot be combined with other selections.");

                parent.MarkIncludeAllScalars();
                hasSelection = true;
                wildcardUsed = true;
                continue;
            }

            if (wildcardUsed)
                throw new DslParseException(
                    "Root wildcard cannot be combined with other selections.");

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && span[i] == '.')
                throw new DslParseException(
                    "Property wildcard syntax is not supported.");

            if (i < span.Length && span[i] == '(')
            {
                i++;

                int parenStart = i;
                int parenDepth = 1;
                while (i < span.Length && parenDepth > 0)
                {
                    if (span[i] == '(') parenDepth++;
                    else if (span[i] == ')') parenDepth--;
                    i++;
                }

                if (parenDepth > 0)
                    throw new DslParseException(
                        "Missing closing parenthesis in 'select' parameter.");

                var innerSpan = span.Slice(parenStart, i - parenStart - 1);
                var childNode = parent.GetOrAddChild(identifier);

                bool innerHasContent = false;
                foreach (var c in innerSpan)
                {
                    if (!char.IsWhiteSpace(c) && c != ',')
                    {
                        innerHasContent = true;
                        break;
                    }
                }

                if (!innerHasContent)
                    throw new DslParseException(
                        $"Empty selection list for '{identifier}'. Expected at least one field.");

                ParseSelectionList(innerSpan, childNode, isRoot: false);
            }
            else
            {
                parent.GetOrAddChild(identifier);
            }

            hasSelection = true;
        }

        if (isRoot && !hasSelection)
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");
    }
}
