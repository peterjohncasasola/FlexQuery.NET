using System.Text.Json;
using DynamicQueryable.Models;

namespace DynamicQueryable.Helpers;

/// <summary>
/// Combines different Select formats (flat dot-notation, nested JSON, includes) 
/// into a unified tree structure for the ProjectionBuilder.
/// </summary>
public static class SelectTreeBuilder
{
    public static Dictionary<string, object> Build(QueryOptions options)
    {
        var tree = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // 1. JSON Select Tree
        if (options.SelectTree != null)
        {
            MergeTree(tree, options.SelectTree);
        }

        // 2. Flat dot-notation paths (e.g. "Id", "Profile.Name")
        if (options.Select != null)
        {
            foreach (var path in options.Select)
            {
                MergePath(tree, path);
            }
        }

        // 3. Includes (e.g. "Orders", "Profile")
        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                MergePath(tree, include);
            }
        }

        return tree;
    }

    private static void MergePath(Dictionary<string, object> current, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        var node = current;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i == parts.Length - 1)
            {
                // Leaf node
                if (!node.ContainsKey(part))
                {
                    node[part] = null!;
                }
            }
            else
            {
                if (!node.TryGetValue(part, out var child) || child == null)
                {
                    child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    node[part] = child;
                }
                node = (Dictionary<string, object>)child;
            }
        }
    }

    private static void MergeTree(Dictionary<string, object> target, Dictionary<string, object> source)
    {
        foreach (var kvp in source)
        {
            if (kvp.Value is Dictionary<string, object> sourceChild)
            {
                if (!target.TryGetValue(kvp.Key, out var targetChild) || targetChild == null)
                {
                    targetChild = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    target[kvp.Key] = targetChild;
                }
                MergeTree((Dictionary<string, object>)targetChild, sourceChild);
            }
            else
            {
                if (!target.ContainsKey(kvp.Key))
                {
                    target[kvp.Key] = null!;
                }
            }
        }
    }

    public static Dictionary<string, object> ParseJsonSelect(JsonElement element)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    dict[prop.Name] = ParseJsonSelect(prop.Value);
                }
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.String)
                {
                    dict[prop.Name] = null!;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    dict[item.GetString()!] = null!;
            }
        }
        return dict;
    }
}
