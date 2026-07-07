using System.Text.Json;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Helpers;

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
            foreach (var path in options.Select)
            {
                MergePath(root, path, includeAllScalarsAtLeaf: false);
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

    /// <summary>Parses a JSON select element into a <see cref="SelectionNode"/> tree.</summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <returns>A <see cref="SelectionNode"/> representing the JSON selection.</returns>
    public static SelectionNode ParseJsonSelect(JsonElement element)
    {
        var node = new SelectionNode();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    MergeTree(node.GetOrAddChild(prop.Name), ParseJsonSelect(prop.Value));
                }
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.String)
                {
                    node.GetOrAddChild(prop.Name);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    node.GetOrAddChild(item.GetString()!);
            }
        }

        return node;
    }
}
