using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Internal;

/// <summary>
/// Helpers for working with the unified <see cref="IncludeNode"/> tree produced by the
/// <c>include</c> query option. The tree is the single source of truth for relationship
/// inclusion: an option-less node includes a navigation with default behavior, while a
/// node carrying filter/sort/take options applies relationship-scoped query operations.
/// </summary>
internal static class IncludeTree
{
    /// <summary>True when this node carries any relationship query option (filter, sort, or take).</summary>
    public static bool HasOptions(this IncludeNode node)
        => node is not null
           && (node.Filter is not null || node.Sort is { Count: > 0 } || node.Take.HasValue);

    /// <summary>True when any node in the subtree (including the node itself) carries options.</summary>
    public static bool HasOptionsAnywhere(this IncludeNode node)
        => node.HasOptions()
           || node.Children.Any(c => c.HasOptionsAnywhere());

    /// <summary>True when the tree has at least one root node.</summary>
    public static bool Any(List<IncludeNode>? tree)
        => tree is { Count: > 0 };

    /// <summary>
    /// Enumerates the full dotted path of every node in the tree, parent before child
    /// (e.g. <c>orders</c>, <c>orders.items</c>). Replaces the flat include path lists the
    /// pipeline previously carried alongside the expansion tree.
    /// </summary>
    public static IEnumerable<string> FlattenPaths(List<IncludeNode>? tree)
    {
        if (tree is null)
            yield break;

        foreach (var node in tree)
        {
            foreach (var path in FlattenPaths(node, string.Empty))
                yield return path;
        }
    }

    private static IEnumerable<string> FlattenPaths(IncludeNode node, string parentPath)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";
        yield return fullPath;

        foreach (var child in node.Children)
        {
            foreach (var path in FlattenPaths(child, fullPath))
                yield return path;
        }
    }

    /// <summary>
    /// Merges <paramref name="incoming"/> into <paramref name="existing"/>: options that
    /// are not yet set are copied, and children are merged by name (case-insensitive),
    /// recursively. Mirrors the parser normalizer's merge semantics.
    /// </summary>
    public static void Merge(IncludeNode existing, IncludeNode incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        existing.Filter ??= incoming.Filter;
        existing.Sort ??= incoming.Sort;
        existing.Take ??= incoming.Take;
        existing.IsExplicitlyRequested |= incoming.IsExplicitlyRequested;

        foreach (var child in incoming.Children)
        {
            var match = existing.Children.FirstOrDefault(c =>
                c.Path.Equals(child.Path, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                Merge(match, child);
            else
                existing.Children.Add(child);
        }
    }

    /// <summary>
    /// Splits dotted paths on programmatically constructed nodes (e.g. from the fluent
    /// builder or JSON request body) into the hierarchical chain the parser would produce,
    /// attaching options to the deepest segment.
    /// </summary>
    public static List<IncludeNode> SplitDottedPaths(List<IncludeNode> nodes)
    {
        var result = new List<IncludeNode>(nodes.Count);
        foreach (var node in nodes)
        {
            result.Add(SplitDotted(node, node.Children));
        }

        return result;

        static IncludeNode SplitDotted(IncludeNode node, List<IncludeNode> children)
        {
            var segments = node.Path
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length <= 1)
                return node;

            var root = new IncludeNode { Path = segments[0], IsExplicitlyRequested = false };
            var current = root;
            for (var i = 1; i < segments.Length; i++)
            {
                var next = new IncludeNode
                {
                    Path = segments[i],
                    IsExplicitlyRequested = i == segments.Length - 1 && node.IsExplicitlyRequested
                };
                current.Children.Add(next);
                current = next;
            }

            if (node.HasOptions())
            {
                current.Filter = node.Filter;
                current.Sort = node.Sort;
                current.Take = node.Take;
            }

            foreach (var child in children)
                current.Children.Add(SplitDotted(child, child.Children));

            return root;
        }
    }
}
