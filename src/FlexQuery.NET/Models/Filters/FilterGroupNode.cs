namespace FlexQuery.NET.Models.Filters;

/// <summary>
/// A group of filter nodes joined by a logical operator.
/// </summary>
public sealed class FilterGroupNode : FilterNode
{
    /// <summary>How to combine the items in this group.</summary>
    public LogicOperator Logic { get; set; } = LogicOperator.And;

    /// <summary>Child nodes (can be groups or conditions).</summary>
    public List<FilterNode> Children { get; set; } = new();
}