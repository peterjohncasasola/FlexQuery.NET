namespace FlexQuery.NET.Models;

/// <summary>
/// Base class for all filter nodes in the query tree.
/// </summary>
public abstract class FilterNode
{
    /// <summary>Whether this filter node should be negated.</summary>
    public bool IsNegated { get; set; }
}

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

/// <summary>
/// A single field-level filter condition.
/// </summary>
public sealed class FilterConditionNode : FilterNode
{
    /// <summary>The property name to filter on.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The comparison operator.</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>The value to compare against.</summary>
    public string? Value { get; set; }

    /// <summary>Nested scoped filter for collection navigations.</summary>
    public FilterGroupNode? ScopedFilter { get; set; }
}
