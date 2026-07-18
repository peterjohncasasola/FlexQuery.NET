using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>
/// Parses DSL SELECT expressions into a list of <see cref="SelectNode"/> projections.
/// Nested selection syntax such as <c>Customer(Id,Name)</c> is represented via
/// <see cref="SelectNode.Children"/>, producing a single canonical parser AST.
/// </summary>
internal static class DslSelectParser
{
    /// <summary>
    /// Parses a DSL select string into a <see cref="List{SelectNode}"/>.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                $"The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (rawSelect.Contains('(') || rawSelect.Contains('*'))
        {
            var children = new List<SelectNode>();
            ParseSelectionList(rawSelect.AsSpan(), children, isRoot: true);
            options.Select = children;
            return;
        }

        var fields = ParserUtilities.SplitCsv(rawSelect);
        var validated = new List<SelectNode>(fields.Count);
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

                if (!ParserUtilities.IsValidPropertyPath(rawPath.AsSpan()))
                    throw new DslParseException(
                        $"Invalid property path '{rawPath}' in 'select' parameter. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

                if (!ParserUtilities.IsValidIdentifier(rawAlias.AsSpan()))
                    throw new DslParseException(
                        $"Invalid alias '{rawAlias}' in 'select' parameter. " +
                        "Aliases must be valid identifiers (e.g. 'FullName').");

                validated.Add(new SelectNode { Field = rawPath, Alias = rawAlias });
                continue;
            }

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new DslParseException(
                    $"Invalid property path '{field}' in 'select' parameter. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').");

            validated.Add(new SelectNode { Field = field });
        }

        options.Select = validated;
    }

    /// <summary>
    /// Parses a DSL select string into a <see cref="SelectionNode"/> tree.
    /// Backward-compatible helper that converts the canonical <see cref="SelectNode"/> AST.
    /// </summary>
    /// <param name="rawSelect">The raw select string.</param>
    /// <returns>A <see cref="SelectionNode"/> representing the selection tree.</returns>
    public static SelectionNode? ParseToSelectionTree(string? rawSelect)
    {
        if (rawSelect is not null && string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");

        var children = new List<SelectNode>();
        ParseSelectionList(rawSelect.AsSpan(), children, isRoot: true);

        var root = new SelectionNode();
        foreach (var node in children)
            MergeIntoSelectionNode(node, root);

        return root;
    }

    private static void MergeIntoSelectionNode(SelectNode source, SelectionNode parent)
    {
        if (source.Field == "*")
        {
            parent.MarkIncludeAllScalars();
            return;
        }

        var parts = source.Field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var node = parent;
        foreach (var part in parts)
        {
            node = node.GetOrAddChild(part);
        }
        
        if (!string.IsNullOrEmpty(source.Alias))
            node.Alias = source.Alias;

        if (source.Children.Count > 0)
        {
            foreach (var nested in source.Children)
                MergeIntoSelectionNode(nested, node);
        }
    }

    private static void ParseSelectionList(ReadOnlySpan<char> span, List<SelectNode> children, bool isRoot)
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

                if (identifier != "*" && !ParserUtilities.IsValidIdentifier(identifier.AsSpan()))
                    throw new DslParseException(
                        $"Invalid identifier '{identifier}' in 'select' parameter. " +
                        "Identifiers must start with a letter and contain only letters, digits, and underscores.");
            }

            if (identifier == "*")
            {
                if (!isRoot)
                    throw new DslParseException(
                        "Wildcard selection is only supported at the root level.");

                if (hasSelection)
                    throw new DslParseException(
                        "Root wildcard cannot be combined with other selections.");

                children.Add(new SelectNode { Field = "*" });
                hasSelection = true;
                wildcardUsed = true;
                continue;
            }

            if (wildcardUsed)
                throw new DslParseException(
                    "Root wildcard cannot be combined with other selections.");

            // At the root level, continue reading dotted property paths
            // so that mixed flat-path and nested syntax can coexist.
            if (isRoot)
            {
                while (i < span.Length && span[i] == '.')
                {
                    i++; // skip dot

                    if (i < span.Length && span[i] == '*')
                    {
                        throw new DslParseException(
                            "Property wildcard syntax is not supported.");
                    }

                    var segmentStart = i;
                    while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                        i++;

                    if (i == segmentStart)
                        throw new DslParseException(
                            "Empty path segment in 'select' parameter. " +
                            "Property paths must be dot-separated identifiers.");

                    identifier += "." + span.Slice(segmentStart, i - segmentStart).ToString();
                }
            }

            string alias = null;

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && span[i] == ':')
            {
                i++;

                int aliasStart = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                    i++;

                if (i == aliasStart)
                    throw new DslParseException(
                        "Empty alias in 'select' parameter. Alias must not be empty (e.g. 'Name:FullName').");

                alias = span.Slice(aliasStart, i - aliasStart).ToString();

                if (string.Equals(alias, "AS", StringComparison.OrdinalIgnoreCase))
                    throw new DslParseException(
                        "Invalid alias in 'select' parameter. " +
                        "The identifier 'AS' is a reserved keyword and cannot be used as an alias.");

                if (!ParserUtilities.IsValidIdentifier(alias.AsSpan()))
                    throw new DslParseException(
                        $"Invalid alias '{alias}' in 'select' parameter. " +
                        "Aliases must be valid identifiers (e.g. 'FullName').");
            }

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && char.IsLetterOrDigit(span[i]))
                throw new DslParseException(
                    "Invalid identifier in 'select' parameter. " +
                    "Identifiers must not contain whitespace or other separators.");

            if (!isRoot && i < span.Length && span[i] == '.')
                throw new DslParseException(
                    "Property wildcard syntax is not supported.");

            if (i < span.Length && span[i] == '(')
            {
                if (alias != null)
                    throw new DslParseException(
                        "Navigation alias is not supported. " +
                        "Navigation nodes represent traversal and cannot be aliased.");

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
                var childNode = new SelectNode { Field = identifier };

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

                ParseSelectionList(innerSpan, childNode.Children, isRoot: false);
                children.Add(childNode);
            }
            else
            {
                var childNode = new SelectNode { Field = identifier, Alias = alias };
                children.Add(childNode);
            }

            hasSelection = true;
        }

        if (isRoot && !hasSelection)
            throw new DslParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.");
    }
}
