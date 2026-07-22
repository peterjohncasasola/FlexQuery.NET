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
        var result = method.Invoke(null, new object[] { query, selector });

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
