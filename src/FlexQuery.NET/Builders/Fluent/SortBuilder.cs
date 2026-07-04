using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Builds a list of SortNode entries for use with QueryBuilder.Sort.</summary>
public sealed class SortBuilder
{
    private readonly List<SortNode> _sorts = [];

    internal List<SortNode> Build() => _sorts;

    /// <summary>Adds an ascending sort on the specified field.</summary>
    public SortBuilder Ascending(string field)
    {
        _sorts.Add(new SortNode { Field = field, Descending = false });
        return this;
    }

    /// <summary>Adds a descending sort on the specified field.</summary>
    public SortBuilder Descending(string field)
    {
        _sorts.Add(new SortNode { Field = field, Descending = true });
        return this;
    }
}
