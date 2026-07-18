using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL SELECT expressions into a list of <see cref="SelectNode"/> projections.
/// Flat selections use property paths with dot notation. Nested selections such as
/// <c>Customer(Id,Name)</c> are represented via <see cref="SelectNode.Children"/>.
/// </summary>
internal static class FqlSelectParser
{
    /// <summary>
    /// Parses an FQL select string into a <see cref="List{SelectNode}"/>.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        
        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.",
                position: -1);

        if (rawSelect.Contains('(') || rawSelect.Contains('*'))
        {
            var children = new List<SelectNode>();
            ParseSelectionList(rawSelect.AsSpan(), children, isRoot: true);
            options.Select = children;
            return;
        }

        var fields = ParserUtilities.SplitCsv(rawSelect);
        var selectedFields = new List<SelectNode>(fields.Count);
        foreach (var field in fields)
        {
            var asIndex = field.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex >= 0)
            {
                var rawPath = field[..asIndex].Trim();
                var rawAlias = field[(asIndex + 3)..].Trim();

                if (rawPath.Length == 0)
                    throw new FqlParseException(
                        "Invalid property path in 'select' parameter. Property path must not be empty (e.g. 'Name AS FullName').",
                        position: -1);

                if (rawAlias.Length == 0)
                    throw new FqlParseException(
                        "Invalid alias in 'select' parameter. Alias must be a non-empty identifier (e.g. 'Name AS FullName').",
                        position: -1);

                if (!ParserUtilities.IsValidPropertyPath(rawPath.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid property path '{rawPath}' in 'select' parameter. Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').",
                        position: -1);

                if (!ParserUtilities.IsValidIdentifier(rawAlias.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid alias '{rawAlias}' in 'select' parameter. Aliases must be valid identifiers (e.g. 'FullName').",
                        position: -1);

                selectedFields.Add(new SelectNode { Field = rawPath, Alias = rawAlias });
                continue;
            }

            if (field.Contains(':'))
                throw new FqlParseException(
                    "Invalid alias in 'select' parameter. FQL does not support colon-separated aliases. Use 'AS' syntax instead (e.g. 'Name AS FullName').",
                    position: -1);

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new FqlParseException(
                    $"Invalid property path '{field}' in 'select' parameter. Property paths must be dot-separated identifiers (e.g. 'Id' or 'Customer.Name').",
                    position: -1);

            selectedFields.Add(new SelectNode { Field = field });
        }

        options.Select = selectedFields;
    }

    /// <summary>
    /// Parses an FQL select string into a <see cref="SelectionNode"/> tree.
    /// Backward-compatible helper that converts the canonical <see cref="SelectNode"/> AST.
    /// </summary>
    public static SelectionNode? ParseToSelectionTree(string? rawSelect)
    {
        
        if (string.IsNullOrWhiteSpace(rawSelect))
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.",
                position: -1);

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
                        "Empty field in 'select' parameter. Expected identifier before comma.",
                        position: -1);

                i++;

                var j = i;
                while (j < span.Length && char.IsWhiteSpace(span[j]))
                    j++;

                if (j >= span.Length)
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Trailing comma is not allowed.",
                        position: -1);

                if (span[j] == ',')
                    throw new FqlParseException(
                        "Empty field in 'select' parameter. Expected identifier between commas.",
                        position: -1);

                continue;
            }

            if (isRoot && span[i] == ')')
                throw new FqlParseException(
                    "Unexpected closing parenthesis in 'select' parameter.",
                    position: -1);

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
                        "Empty field in 'select' parameter. Expected identifier or '*'.",
                        position: -1);

                identifier = span.Slice(start, i - start).ToString();

                if (identifier != "*" && !ParserUtilities.IsValidIdentifier(identifier.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid identifier '{identifier}' in 'select' parameter. " +
                        "Identifiers must start with a letter and contain only letters, digits, and underscores.",
                        position: -1);
            }

            if (identifier == "*")
            {
                if (!isRoot)
                    throw new FqlParseException(
                        "Wildcard selection is only supported at the root level.",
                        position: -1);

                if (hasSelection)
                    throw new FqlParseException(
                        "Root wildcard cannot be combined with other selections.",
                        position: -1);

                children.Add(new SelectNode { Field = "*" });
                hasSelection = true;
                wildcardUsed = true;
                continue;
            }

            if (wildcardUsed)
                throw new FqlParseException(
                    "Root wildcard cannot be combined with other selections.",
                    position: -1);

            // At the root level, continue reading dotted property paths
            // so that mixed flat-path and nested syntax can coexist.
            if (isRoot)
            {
                while (i < span.Length && span[i] == '.')
                {
                    i++; // skip dot

                    if (i < span.Length && span[i] == '*')
                    {
                        throw new FqlParseException(
                            "Property wildcard syntax is not supported.",
                            position: -1);
                    }

                    var segmentStart = i;
                    while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                        i++;

                    if (i == segmentStart)
                        throw new FqlParseException(
                            "Empty path segment in 'select' parameter. " +
                            "Property paths must be dot-separated identifiers.",
                            position: -1);

                    identifier += "." + span.Slice(segmentStart, i - segmentStart).ToString();
                }
            }

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
                        "Empty alias in 'select' parameter. Alias must not be empty (e.g. 'Name AS FullName').",
                        position: -1);

                alias = span.Slice(aliasStart, i - aliasStart).ToString();

                if (!ParserUtilities.IsValidIdentifier(alias.AsSpan()))
                    throw new FqlParseException(
                        $"Invalid alias '{alias}' in 'select' parameter. Aliases must be valid identifiers (e.g. 'FullName').",
                        position: -1);
            }

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && char.IsLetterOrDigit(span[i]))
                throw new FqlParseException(
                    "Invalid identifier in 'select' parameter. Identifiers must not contain whitespace or other separators.",
                    position: -1);

            if (!isRoot && i < span.Length && span[i] == '.')
                throw new FqlParseException(
                    "Property wildcard syntax is not supported.",
                    position: -1);

            if (i < span.Length && span[i] == '(')
            {
                if (alias != null)
                    throw new FqlParseException(
                        "Navigation alias is not supported. Navigation nodes represent traversal and cannot be aliased.",
                        position: -1);

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
                        "Missing closing parenthesis in 'select' parameter.",
                        position: -1);

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
                    throw new FqlParseException(
                        $"Empty selection list for '{identifier}'. Expected at least one field.",
                        position: -1);

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
            throw new FqlParseException(
                "The 'select' parameter value is empty. Expected comma-separated field paths.",
                position: -1);
    }
}
