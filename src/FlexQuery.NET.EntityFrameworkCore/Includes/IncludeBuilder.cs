using System.Linq.Expressions;
using FlexQuery.NET.Internal;
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

        var rootType = typeof(T);
        foreach (var node in options.Includes)
            query = ApplyNode(query, rootType, node, IncludeContext.Root, options);

        return query;
    }

    /// <summary>
    /// Builds the hierarchical relationship-query window tree from the parsed include tree,
    /// keyed by the root navigation property name (entity-level names — translation has
    /// already run). Deep paths are supported: child windows ride along inside the
    /// parent window node and are applied by the projection builders per level, keeping
    /// nested collection windows correlated to their already-selected parent.
    /// </summary>
    public static bool TryBuildIncludeWindows(
        QueryOptions options,
        out Dictionary<string, IncludeWindowNode> windows)
    {
        windows = new Dictionary<string, IncludeWindowNode>(StringComparer.OrdinalIgnoreCase);

        if (options.Includes is not { Count: > 0 })
            return false;

        foreach (var node in options.Includes)
        {
            // Only relationship query blocks produce windows; plain includes keep the
            // provider's default full-navigation projection.
            if (node.HasOptionsAnywhere() || node.Children.Count > 0)
                windows[node.Path] = BuildWindowNode(node);
        }

        return windows.Count > 0;
    }

    private static IncludeWindowNode BuildWindowNode(IncludeNode node)
    {
        var windowNode = new IncludeWindowNode { Node = node };

        foreach (var child in node.Children)
        {
            windowNode.Children[child.Path] = BuildWindowNode(child);
        }

        return windowNode;
    }

    /// <summary>
    /// Determines whether the include tree contains at least one nested (deep) level —
    /// i.e. any root include carries child windows. Deep levels are applied through
    /// the EF filtered-include chain (correlated per parent element), which is the only
    /// portable correlated per-parent Take form; the root level is applied server-side
    /// inside the DTO projection.
    /// </summary>
    public static bool HasDeepWindows(QueryOptions options)
        => options.Includes is { Count: > 0 }
           && options.Includes.Any(e => e.Children is { Count: > 0 });

    /// <summary>
    /// True when any include path at any level carries take/sort/filter options. Relationship
    /// query blocks are applied through the EF filtered-include chain (correlated per parent;
    /// the provider renders its canonical windowed shape) followed by client-side DTO
    /// mapping — the in-projection ordered child Take form is not translatable on all
    /// providers (APPLY on SQLite).
    /// </summary>
    public static bool HasIncludeOptions(QueryOptions options)
        => options.Includes is { Count: > 0 }
           && options.Includes.Any(e => e.HasOptionsAnywhere());

    /// <summary>
    /// Builds root-level windows (include paths without children) for the
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

        var rootType = typeof(T);
        foreach (var node in options.Includes)
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
