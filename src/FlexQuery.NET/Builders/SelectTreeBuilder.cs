using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Combines different select formats into a merged navigation tree.
/// </summary>
internal static class SelectTreeBuilder
{
    /// <summary>
    /// Builds a merged <see cref="SelectionNode"/> tree from all select-related properties
    /// in <paramref name="options"/>: SelectTree, Select, Includes, and FilteredIncludes.
    /// </summary>
    /// <param name="options">The query options to build the selection tree from.</param>
    /// <returns>A merged <see cref="SelectionNode"/> representing the full selection.</returns>
    public static SelectionNode Build(QueryOptions options)
    {
        var root = new SelectionNode();

        if (options.Select is { Count: > 0 })
        {
            foreach (var model in options.Select)
            {
                MergePath(root, model, includeAllScalarsAtLeaf: false);
            }
        }
        else if (options.SelectTree != null)
        {
            MergeTree(root, options.SelectTree);
        }

        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                MergePath(root, include, includeAllScalarsAtLeaf: true);
            }
            
            if (options.Select == null && options.SelectTree == null)
            {
                root.MarkIncludeAllScalars();
            }
        }

        if (options.Expand != null)
        {
            foreach (var node in options.Expand)
            {
                MergeIncludeNode(root, node);
            }

            if (options.Select == null && options.SelectTree == null)
            {
                root.MarkIncludeAllScalars();
            }
        }

        return root;
    }

    private static void MergePath(SelectionNode current, SelectNode? model, bool includeAllScalarsAtLeaf)
    {
        if (model == null) return;
        
        var field = model.Field;
        if (string.IsNullOrWhiteSpace(field)) return;

        if (field == "*")
        {
            current.MarkIncludeAllScalars();
            return;
        }

        var alias = model.Alias;
        bool isWildcard = field.EndsWith(".*");
        if (isWildcard)
        {
            field = field.Substring(0, field.Length - 2);
        }

        var parts = field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var node = current;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            node = node.GetOrAddChild(part);
            if (i == parts.Length - 1)
            {
                ApplyAlias(node, alias, model.Field);
            }
        }

        if (isWildcard)
        {
            node.MarkIncludeAllScalars();
        }
        else if (includeAllScalarsAtLeaf && !node.HasChildren)
        {
            node.MarkIncludeAllScalars();
        }

        MergeChildren(node, model.Children, includeAllScalarsAtLeaf: false, parentPath: model.Field);
    }

    /// <summary>
    /// Merges already-parsed child <see cref="SelectNode"/> entries into <paramref name="parent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parser invariant: nested <see cref="SelectNode.Children"/> are produced exclusively by
    /// <see cref="Parsers.Fql.FqlSelectParser"/> and <see cref="Parsers.Dsl.DslSelectParser"/>.
    /// Both parsers enforce that child fields inside parentheses are simple identifiers—dotted paths
    /// and wildcards are explicitly rejected. Therefore <paramref name="children"/> may only contain
    /// simple names, and this method must never re-parse <c>child.Field</c> as a dotted path.
    /// </para>
    /// </remarks>
    private static void MergeChildren(SelectionNode parent, IReadOnlyList<SelectNode> children, bool includeAllScalarsAtLeaf, string parentPath)
    {
        foreach (var child in children)
        {
            var childNode = parent.GetOrAddChild(child.Field);
            var fullyQualified = string.IsNullOrEmpty(parentPath) ? child.Field : $"{parentPath}.{child.Field}";
            ApplyAlias(childNode, child.Alias, fullyQualified);
            MergeChildren(childNode, child.Children, includeAllScalarsAtLeaf: false, parentPath: fullyQualified);
        }
    }

    private static void ApplyAlias(SelectionNode node, string? alias, string fullyQualifiedField)
    {
        if (string.IsNullOrWhiteSpace(alias)) return;
        
        if (!string.IsNullOrWhiteSpace(node.Alias) &&
            !string.Equals(node.Alias, alias, StringComparison.Ordinal))
        {
            throw new QueryValidationException(
                $"The field '{fullyQualifiedField}' is projected multiple times with different aliases " +
                $"('{node.Alias}' and '{alias}'). A source field may only be projected once per query.");
        }
        node.Alias = alias;
    }

    private static void MergePath(SelectionNode current, string path, bool includeAllScalarsAtLeaf)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        
        var aliasParts = System.Text.RegularExpressions.Regex.Split(path, @"\s+as\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var actualPath = aliasParts[0].Trim();
        var alias = aliasParts.Length > 1 ? aliasParts[1].Trim() : null;

        bool isWildcard = actualPath.EndsWith(".*");
        if (isWildcard)
        {
            actualPath = actualPath.Substring(0, actualPath.Length - 2);
        }

        var parts = actualPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var node = current;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            node = node.GetOrAddChild(part);
            if (i == parts.Length - 1)
            {
                ApplyAlias(node, alias, path);
            }
        }

        if (isWildcard)
        {
            node.MarkIncludeAllScalars();
        }
        else if (includeAllScalarsAtLeaf && !node.HasChildren)
        {
            node.MarkIncludeAllScalars();
        }
    }

    private static void MergeTree(SelectionNode target, SelectionNode source, string parentPath = "")
    {
        if (source.IncludeAllScalars)
        {
            target.MarkIncludeAllScalars();
        }

        foreach (var kvp in source.EnumerateChildren())
        {
            var targetChild = target.GetOrAddChild(kvp.Key);
            var fullyQualified = string.IsNullOrEmpty(parentPath) ? kvp.Key : $"{parentPath}.{kvp.Key}";
            ApplyAlias(targetChild, kvp.Value.Alias, fullyQualified);
            MergeTree(targetChild, kvp.Value, fullyQualified);
        }
    }

    private static void MergeIncludeNode(SelectionNode target, IncludeNode source)
    {
        var node = target.GetOrAddChild(source.Path);
        
        if (!node.HasChildren)
        {
            node.MarkIncludeAllScalars();
        }
        
        if (source.Filter != null)
        {
            node.Filter = source.Filter;
        }

        foreach (var child in source.Children)
        {
            MergeIncludeNode(node, child);
        }
    }

}
