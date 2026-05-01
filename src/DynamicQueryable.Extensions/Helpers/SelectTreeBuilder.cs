using System.Text.Json;
using DynamicQueryable.Models;

namespace DynamicQueryable.Helpers;

/// <summary>
/// Combines different select formats into a merged navigation tree.
/// </summary>
internal static class SelectTreeBuilder
{
    public static SelectionNode Build(QueryOptions options)
    {
        var root = new SelectionNode();

        // 1. JSON Select Tree
        if (options.SelectTree != null)
        {
            MergeTree(root, options.SelectTree);
        }

        // 2. Flat dot-notation paths (e.g. "Id", "Profile.Name")
        if (options.Select != null)
        {
            foreach (var path in options.Select)
            {
                MergePath(root, path, includeAllScalarsAtLeaf: false);
            }
        }

        // 3. Includes (e.g. "Orders", "Profile")
        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                MergePath(root, include, includeAllScalarsAtLeaf: true);
            }
            
            // If only includes are present (no Select), mark root to include all root scalars
            if (options.Select == null && options.SelectTree == null)
            {
                root.MarkIncludeAllScalars();
            }
        }

        // 4. Filtered Includes (e.g. "Orders(status = 'cancelled')")
        if (options.FilteredIncludes != null)
        {
            foreach (var node in options.FilteredIncludes)
            {
                MergeIncludeNode(root, node);
            }
        }

        return root;
    }

    private static void MergePath(SelectionNode current, string path, bool includeAllScalarsAtLeaf)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var node = current;
        foreach (var part in parts)
        {
            node = node.GetOrAddChild(part);
        }

        if (includeAllScalarsAtLeaf)
        {
            node.MarkIncludeAllScalars();
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
        node.MarkIncludeAllScalars();
        
        if (source.Filter != null)
        {
            node.Filter = source.Filter;
        }

        foreach (var child in source.Children)
        {
            MergeIncludeNode(node, child);
        }
    }

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
