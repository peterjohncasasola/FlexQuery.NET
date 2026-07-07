using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Builds filtered include trees (FilteredIncludes) for navigation expansion.</summary>
public sealed class ExpandBuilder
{
    private readonly List<IncludeNode> _includes = [];

    internal List<IncludeNode> Build() => _includes;

    /// <summary>Adds a navigation path with optional filter and child expansions.</summary>
    /// <param name="path">The navigation property path (e.g. "Orders.Items").</param>
    /// <param name="filter">Optional filter to apply on the navigation.</param>
    /// <param name="configureChildren">Optional nested expansions on the navigation.</param>
    public ExpandBuilder Path(string path, Action<FilterGroupBuilder>? filter = null, Action<ExpandBuilder>? configureChildren = null)
    {
        var node = new IncludeNode { Path = path };

        if (filter is not null)
        {
            var fb = new FilterGroupBuilder();
            filter(fb);
            node.Filter = fb.Build();
        }

        if (configureChildren is not null)
        {
            var child = new ExpandBuilder();
            configureChildren(child);
            node.Children = child._includes;
        }

        _includes.Add(node);
        return this;
    }
}
