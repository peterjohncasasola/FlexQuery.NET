using System.Linq.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

internal static class IncludeBuilder
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, QueryOptions options)
        where T : class
    {
        if (options.Includes is not { Count: > 0 })
            return query;

        var includeTree = BuildTreeFromIncludes(options.Includes);
        MergeExpandConfiguration(includeTree, options.Expand);

        var rootType = typeof(T);
        foreach (var node in includeTree)
            query = ApplyNode(query, rootType, node, IncludeContext.Root, options);

        return query;
    }

    /// <summary>
    /// Builds the hierarchical expansion window tree from the normalized expand nodes,
    /// keyed by the root navigation property name (entity-level names — translation has
    /// already run). Deep paths are supported: child windows ride along inside the
    /// parent window node and are applied by the projection builders per level, keeping
    /// nested collection windows correlated to their already-selected parent.
    /// </summary>
    public static bool TryBuildExpandTree(
        QueryOptions options,
        out Dictionary<string, ExpandWindowNode> expandTree)
    {
        expandTree = new Dictionary<string, ExpandWindowNode>(StringComparer.OrdinalIgnoreCase);

        if (options.Expand is not { Count: > 0 })
            return false;

        foreach (var node in options.Expand)
        {
            expandTree[node.Path] = BuildWindowNode(node);
        }

        return expandTree.Count > 0;
    }

    private static ExpandWindowNode BuildWindowNode(IncludeNode node)
    {
        var windowNode = new ExpandWindowNode { Node = node };

        foreach (var child in node.Children)
        {
            windowNode.Children[child.Path] = BuildWindowNode(child);
        }

        return windowNode;
    }

    /// <summary>
    /// Determines whether the expand tree contains at least one nested (deep) level —
    /// i.e. any root expansion carries child windows. Deep levels are applied through
    /// the EF filtered-include chain (correlated per parent element), which is the only
    /// portable correlated per-parent Take form; the root level is applied server-side
    /// inside the DTO projection.
    /// </summary>
    public static bool HasDeepWindows(QueryOptions options)
        => options.Expand is { Count: > 0 }
           && options.Expand.Any(e => e.Children is { Count: > 0 });

    /// <summary>
    /// True when any expansion path carries take/sort/filter options. Expansion windows
    /// are applied through the EF filtered-include chain (correlated per parent; the
    /// provider renders its canonical windowed shape) followed by client-side DTO
    /// mapping — the in-projection ordered child Take form is not translatable on all
    /// providers (APPLY on SQLite).
    /// </summary>
    public static bool HasExpansionOptions(QueryOptions options)
        => options.Expand is { Count: > 0 }
           && options.Expand.Any(e =>
               e.Take.HasValue || e.Filter is not null || e.Sort is { Count: > 0 });

    /// <summary>
    /// Builds root-level expansion windows (expand paths without children) for the
    /// server-side DTO projection. Returns false when there are no root-level windows.
    /// Deep child windows are intentionally excluded — they ride on the EF filtered
    /// include chain (correlated per parent), see <see cref="HasDeepWindows"/>.
    /// </summary>
    public static bool TryBuildRootLevelWindows<T>(
        QueryOptions options,
        out Dictionary<string, LambdaExpression> windows)
        where T : class
    {
        windows = new Dictionary<string, LambdaExpression>(StringComparer.OrdinalIgnoreCase);

        if (options.Includes is not { Count: > 0 })
            return false;

        var includeTree = BuildTreeFromIncludes(options.Includes);
        MergeExpandConfiguration(includeTree, options.Expand);

        var rootType = typeof(T);
        foreach (var node in includeTree)
        {
            var navigation = IncludeNavigationResolver.Resolve(rootType, node.Path);
            if (navigation is null)
                return false;

            var selector = IncludeSelectorFactory.Build(
                rootType, navigation, node, options, allowFilteredCollection: true);

            windows[navigation.Property.Name] = selector;
        }

        return windows.Count > 0;
    }

    private static List<IncludeNode> BuildTreeFromIncludes(List<string> includes)
    {
        var rootNodes = new List<IncludeNode>();
        var lookup = new Dictionary<string, IncludeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var include in includes)
        {
            var segments = include.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
                continue;

            IncludeNode? parent = null;
            string currentPath = "";

            foreach (var segment in segments)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}.{segment}";

                if (!lookup.TryGetValue(currentPath, out var node))
                {
                    node = new IncludeNode { Path = segment };
                    lookup[currentPath] = node;

                    if (parent == null)
                        rootNodes.Add(node);
                    else
                        parent.Children.Add(node);
                }

                parent = node;
            }
        }

        return rootNodes;
    }

    private static void MergeExpandConfiguration(List<IncludeNode> includeTree, List<IncludeNode>? expandTree)
    {
        if (expandTree is not { Count: > 0 })
            return;

        var expandLookup = new Dictionary<string, IncludeNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in expandTree)
            FlattenExpandTree(node, "", expandLookup);

        foreach (var includeNode in includeTree)
            MergeExpandNode(includeNode, "", expandLookup);
    }

    private static void FlattenExpandTree(IncludeNode node, string parentPath, Dictionary<string, IncludeNode> lookup)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";
        lookup[fullPath] = node;

        foreach (var child in node.Children)
            FlattenExpandTree(child, fullPath, lookup);
    }

    private static void MergeExpandNode(IncludeNode includeNode, string parentPath, Dictionary<string, IncludeNode> expandLookup)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? includeNode.Path : $"{parentPath}.{includeNode.Path}";

        if (expandLookup.TryGetValue(fullPath, out var expandNode))
        {
            includeNode.Filter = expandNode.Filter ?? includeNode.Filter;
            includeNode.Sort = expandNode.Sort ?? includeNode.Sort;
            includeNode.Take = expandNode.Take ?? includeNode.Take;
        }

        foreach (var child in includeNode.Children)
            MergeExpandNode(child, fullPath, expandLookup);
    }

    private static IQueryable<T> ApplyNode<T>(
        IQueryable<T> query,
        Type parentType,
        IncludeNode node,
        IncludeContext context,
        QueryOptions options)
        where T : class
    {
        var navigation = IncludeNavigationResolver.Resolve(parentType, node.Path);
        if (navigation is null)
            return query;

        var selector = IncludeSelectorFactory.Build(
            parentType, navigation, node, options, allowFilteredCollection: true);

        var method = IncludeMethodCache.Resolve(context, typeof(T), parentType, selector.ReturnType);
        var result = method.Invoke(null, [query, selector]);

        var typed = (IQueryable<T>)result!;

        if (node.Children is { Count: > 0 })
        {
            var nextContext = navigation.IsCollection
                ? IncludeContext.AfterCollection
                : IncludeContext.AfterReference;

            foreach (var child in node.Children)
                typed = ApplyNode(typed, navigation.TargetType, child, nextContext, options);
        }

        return typed;
    }
}


