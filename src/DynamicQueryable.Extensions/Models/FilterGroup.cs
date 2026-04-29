namespace DynamicQueryable.Models;

/// <summary>Logical operator combining multiple filters or groups.</summary>
public enum LogicOperator
{
    /// <summary>Logical AND.</summary>
    And,
    /// <summary>Logical OR.</summary>
    Or
}

/// <summary>
/// A group of filters joined by a logical operator.
/// Supports nested sub-groups for complex AND/OR trees.
/// </summary>
public sealed class FilterGroup
{
    /// <summary>How to combine the items in this group.</summary>
    public LogicOperator Logic { get; set; } = LogicOperator.And;

    /// <summary>Individual field-level filter conditions.</summary>
    public List<FilterCondition> Filters { get; set; } = [];

    /// <summary>Nested sub-groups (recursive).</summary>
    public List<FilterGroup> Groups { get; set; } = [];
}
