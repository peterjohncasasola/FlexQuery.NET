using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests;

internal static class IncludeTestFactory
{
    public static List<IncludeNode> Paths(params string[] paths)
        => Merge(IncludeTree.SplitDottedPaths(paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new IncludeNode { Path = p.Trim() })
            .ToList()));

    public static string[] PathStrings(List<IncludeNode>? tree)
        => IncludeTree.FlattenPaths(tree).ToArray();

    /// <summary>
    /// Merged view of several include trees: roots are matched by name (case-insensitive)
    /// and options/children are merged, preserving the provenance flags that distinguish
    /// explicitly-requested paths from dotted-path scaffolding.
    /// </summary>
    public static List<IncludeNode> Merge(params List<IncludeNode>[] trees)
    {
        var result = new List<IncludeNode>();

        foreach (var tree in trees)
        {
            foreach (var node in tree)
            {
                var existing = result.FirstOrDefault(r =>
                    r.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                    IncludeTree.Merge(existing, node);
                else
                    result.Add(node);
            }
        }

        return result;
    }

    public static string? MergeExpressions(string? include, string? expand)
    {
        if (string.IsNullOrWhiteSpace(expand)) return include;
        if (string.IsNullOrWhiteSpace(include)) return expand;

        var expEntries = SplitTopLevel(expand);
        var expBases = expEntries
            .Select(e => GetBasePath(e).ToLowerInvariant())
            .ToHashSet();

        var kept = SplitTopLevel(include)
            .Where(e => !expBases.Contains(GetBasePath(e).ToLowerInvariant()))
            .Select(e => e.Trim())
            .ToList();

        kept.AddRange(expEntries.Select(e => e.Trim()));
        return string.Join(",", kept);
    }

    private static IEnumerable<string> SplitTopLevel(string value)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                yield return value[start..i];
                start = i + 1;
            }
        }
        yield return value[start..];
    }

    private static string GetBasePath(string entry)
    {
        var paren = entry.IndexOf('(');
        var b = paren >= 0 ? entry[..paren] : entry;
        return b.Trim();
    }
}
