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

        if (options.SelectTree != null)
        {
            MergeTree(root, options.SelectTree);
        }

        if (options.Select != null)
        {
            foreach (var model in options.Select)
            {
                MergePath(root, model, includeAllScalarsAtLeaf: false);
            }
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

    private static void MergePath(SelectionNode current, SelectModel model, bool includeAllScalarsAtLeaf)
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
            if (i == parts.Length - 1 && alias != null)
            {
                if (!string.IsNullOrWhiteSpace(node.Alias) &&
                    !string.Equals(node.Alias, alias, StringComparison.Ordinal))
                {
                    throw new QueryValidationException(
                        $"Conflicting aliases for field '{model.Field}': '{node.Alias}' and '{alias}'. " +
                        "Use identical aliases for the same field or omit the alias.");
                }
                node.Alias = alias;
            }
        }

        if (isWildcard)
        {
            node.MarkIncludeAllScalars();
        }
        else if (includeAllScalarsAtLeaf)
        {
            if (!node.HasChildren)
            {
                node.MarkIncludeAllScalars();
            }
        }

        foreach (var child in model.Children)
        {
            var childNode = node.GetOrAddChild(child.Field);
            MergePath(childNode, child, includeAllScalarsAtLeaf: false);
        }
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
            if (i == parts.Length - 1 && alias != null)
            {
                if (!string.IsNullOrWhiteSpace(node.Alias) &&
                    !string.Equals(node.Alias, alias, StringComparison.Ordinal))
                {
                    throw new QueryValidationException(
                        $"Conflicting aliases for field '{path}': '{node.Alias}' and '{alias}'. " +
                        "Use identical aliases for the same field or omit the alias.");
                }
                node.Alias = alias;
            }
        }

        if (isWildcard)
        {
            node.MarkIncludeAllScalars();
        }
        else if (includeAllScalarsAtLeaf)
        {
            if (!node.HasChildren)
            {
                node.MarkIncludeAllScalars();
            }
        }
    }

    private static void MergeTree(SelectionNode target, SelectionNode source)
    {
        if (source.IncludeAllScalars)
        {
            target.MarkIncludeAllScalars();
        }

        foreach (var kvp in source.EnumerateChildren())
        {
            var targetChild = target.GetOrAddChild(kvp.Key);

            if (!string.IsNullOrWhiteSpace(kvp.Value.Alias))
            {
                if (!string.IsNullOrWhiteSpace(targetChild.Alias) &&
                    !string.Equals(targetChild.Alias, kvp.Value.Alias, StringComparison.Ordinal))
                {
                    throw new QueryValidationException(
                        $"Conflicting aliases for field '{kvp.Key}': '{targetChild.Alias}' and '{kvp.Value.Alias}'. " +
                        "Use identical aliases for the same field or omit the alias.");
                }
                targetChild.Alias = kvp.Value.Alias;
            }

            MergeTree(targetChild, kvp.Value);
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
