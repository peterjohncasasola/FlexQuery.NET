using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL SELECT expressions into a list of scalar field paths or a SelectionNode tree.
/// </summary>
internal static class FqlSelectParser
{
    /// <summary>
    /// Parses an FQL select string into field paths on options.Select, or into a
    /// SelectionNode tree on options.SelectTree when nested syntax is detected.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (rawSelect is not null && (rawSelect.Contains('(') || rawSelect.Contains('*')))
        {
            options.SelectTree = ParseToSelectionTree(rawSelect);
            options.Select = null;
            return;
        }

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

    /// <summary>
    /// Parses an FQL select string into a <see cref="SelectionNode"/> tree.
    /// Supports recursive nested selection syntax such as <c>Customer(Id,Name)</c>.
    /// </summary>
    public static SelectionNode? ParseToSelectionTree(string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        var root = new SelectionNode();
        var span = rawSelect.AsSpan();

        ParseSelectionList(span, root, isRoot: true);

        return root;
    }

    private static void ParseSelectionList(ReadOnlySpan<char> span, SelectionNode parent, bool isRoot)
    {
        var i = 0;
        var hasSelection = false;
        var wildcardUsed = false;

        while (i < span.Length)
        {
            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i >= span.Length)
                break;

            if (span[i] == ',')
            {
                if (!hasSelection)
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Expected identifier before comma.");

                i++;

                var j = i;
                while (j < span.Length && char.IsWhiteSpace(span[j]))
                    j++;

                if (j >= span.Length)
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Trailing comma is not allowed.");

                if (span[j] == ',')
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Expected identifier between commas.");

                continue;
            }

            if (isRoot && span[i] == ')')
                throw new FqlParseException(
                    "Unexpected closing parenthesis in 'select' parameter.");

            string identifier;
            if (span[i] == '*')
            {
                i++;
                identifier = "*";
            }
            else
            {
                var start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                    i++;

                if (i == start)
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Expected identifier or '*'.");

                identifier = span.Slice(start, i - start).ToString();
            }

            if (identifier == "*")
            {
                if (!isRoot)
                    throw new FqlParseException(
                        "Wildcard selection is only supported at the root level.");

                if (hasSelection)
                    throw new FqlParseException(
                        "Root wildcard cannot be combined with other selections.");

                parent.MarkIncludeAllScalars();
                hasSelection = true;
                wildcardUsed = true;
                continue;
            }

            if (wildcardUsed)
                throw new FqlParseException(
                    "Root wildcard cannot be combined with other selections.");

            string? alias = null;

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (span.Length - i >= 3 &&
                (span[i] == 'A' || span[i] == 'a') &&
                (span[i + 1] == 'S' || span[i + 1] == 's') &&
                char.IsWhiteSpace(span[i + 2]))
            {
                i += 3;

                while (i < span.Length && char.IsWhiteSpace(span[i]))
                    i++;

                var aliasStart = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                    i++;

                if (i == aliasStart)
                    throw new FqlParseException(
                        "Empty alias in 'select' parameter. Alias must not be empty (e.g. 'Name AS FullName').");

                alias = span.Slice(aliasStart, i - aliasStart).ToString();

                if (string.Equals(alias, "AS", StringComparison.OrdinalIgnoreCase))
                    throw new FqlParseException(
                        "Invalid alias in 'select' parameter. " +
                        "The identifier 'AS' is a reserved keyword and cannot be used as an alias.");

                if (!ParserUtilities.IsValidIdentifier(alias.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid alias '{alias}' in 'select' parameter. " +
                        "Aliases must be valid identifiers (e.g. 'FullName').");
            }

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && span[i] == '.')
                throw new FqlParseException(
                    "Property wildcard syntax is not supported.");

            if (i < span.Length && span[i] == '(')
            {
                if (alias != null)
                    throw new FqlParseException(
                        "Navigation alias is not supported. " +
                        "Navigation nodes represent traversal and cannot be aliased.");

                i++;

                var parenStart = i;
                var parenDepth = 1;
                while (i < span.Length && parenDepth > 0)
                {
                    if (span[i] == '(') parenDepth++;
                    else if (span[i] == ')') parenDepth--;
                    i++;
                }

                if (parenDepth > 0)
                    throw new FqlParseException(
                        "Missing closing parenthesis in 'select' parameter.");

                var innerSpan = span.Slice(parenStart, i - parenStart - 1);
                var childNode = parent.GetOrAddChild(identifier);

                var innerHasContent = false;
                foreach (var c in innerSpan)
                {
                    if (!char.IsWhiteSpace(c) && c != ',')
                    {
                        innerHasContent = true;
                        break;
                    }
                }

                if (!innerHasContent)
                    throw new FqlParseException(
                        $"Empty selection list for '{identifier}'. Expected at least one field.");

                ParseSelectionList(innerSpan, childNode, isRoot: false);
            }
            else
            {
                var childNode = parent.GetOrAddChild(identifier);
                if (alias != null)
                    childNode.Alias = alias;
            }

            hasSelection = true;
        }

        if (isRoot && !hasSelection)
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");
    }
}
