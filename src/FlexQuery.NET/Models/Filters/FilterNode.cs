namespace FlexQuery.NET.Models.Filters;

/// <summary>
/// Base class for all filter nodes in the query tree.
/// </summary>
public abstract class FilterNode
{
    /// <summary>Whether this filter node should be negated.</summary>
    public bool IsNegated { get; set; }
}