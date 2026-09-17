using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>
/// Builds relationship-include trees (simple inclusions and relationship-scoped query
/// blocks) for the fluent query builder.
/// </summary>
public sealed class IncludeBuilder
{
    private readonly List<IncludeNode> _includes = [];

    internal List<IncludeNode> Build() => IncludeTree.SplitDottedPaths(_includes);

    /// <summary>
    /// Adds a navigation path with optional relationship query options and child includes.
    /// </summary>
    /// <param name="path">The navigation property path (e.g. "Orders.Items").</param>
    /// <param name="filter">Optional filter to apply to the included collection.</param>
    /// <param name="configureChildren">Optional nested includes on the navigation target.</param>
    /// <param name="take">Optional number of items to take from the included collection.</param>
    /// <param name="sort">Optional sort expressions applied to the included collection.</param>
    /// <remarks>
    /// <paramref name="filter"/>, <paramref name="take"/>, and <paramref name="sort"/> are
    /// collection-only operations; validation rejects them on single-valued relationships.
    /// </remarks>
    public IncludeBuilder Path(
        string path,
        Action<FilterGroupBuilder>? filter = null,
        Action<IncludeBuilder>? configureChildren = null,
        int? take = null,
        List<SortNode>? sort = null)
    {
        var node = new IncludeNode { Path = path, Take = take };

        if (sort is { Count: > 0 })
            node.Sort = sort;

        if (filter is not null)
        {
            var fb = new FilterGroupBuilder();
            filter(fb);
            node.Filter = fb.Build();
        }

        if (configureChildren is not null)
        {
            var child = new IncludeBuilder();
            configureChildren(child);
            node.Children = child.Build();
        }

        _includes.Add(node);
        return this;
    }
}
