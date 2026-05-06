namespace FlexQuery.NET.Models;

/// <summary>
/// Represents a lightweight filter tree used by include/projection validation
/// and collection-scoped filter construction.
/// </summary>
public sealed class FilterGroup
{
    /// <summary>How to combine the filters and sub-groups.</summary>
    public LogicOperator Logic { get; set; } = LogicOperator.And;

    /// <summary>Top-level filter conditions in this group.</summary>
    public List<FilterCondition> Filters { get; set; } = new();

    /// <summary>Child filter groups nested under this group.</summary>
    public List<FilterGroup> Groups { get; set; } = new();

    /// <summary>Whether this group is negated.</summary>
    public bool IsNegated { get; set; }

    /// <summary>
    /// Converts a <see cref="FilterGroup"/> to a <see cref="FilterGroupNode"/> implicitly.
    /// </summary>
    /// <param name="group">The source filter group to convert.</param>
    /// <returns>A <see cref="FilterGroupNode"/> representing the same filter structure as the source group.</returns>
    public static implicit operator FilterGroupNode?(FilterGroup? group)
        => group is null ? null : group.ToFilterGroupNode();

    /// <summary>
    /// Converts a <see cref="FilterGroupNode"/> to a <see cref="FilterGroup"/> implicitly.
    /// </summary>
    /// <param name="group">The source filter group node to convert.</param>
    /// <returns>A <see cref="FilterGroup"/> representing the same filter structure as the source node.</returns>
    public static implicit operator FilterGroup?(FilterGroupNode? group)
        => group is null ? null : FromFilterGroupNode(group);

    private FilterGroupNode ToFilterGroupNode()
    {
        var node = new FilterGroupNode
        {
            Logic = Logic,
            IsNegated = IsNegated
        };

        foreach (var filter in Filters)
        {
            node.Children.Add(new FilterConditionNode
            {
                Field = filter.Field,
                Operator = filter.Operator,
                Value = filter.Value,
                IsNegated = filter.IsNegated,
                ScopedFilter = filter.ScopedFilter
            });
        }

        foreach (var group in Groups)
        {
            node.Children.Add(group);
        }

        return node;
    }

    private static FilterGroup FromFilterGroupNode(FilterGroupNode group)
    {
        var output = new FilterGroup
        {
            Logic = group.Logic,
            IsNegated = group.IsNegated
        };

        foreach (var child in group.Children)
        {
            switch (child)
            {
                case FilterConditionNode condition:
                    output.Filters.Add(new FilterCondition
                    {
                        Field = condition.Field,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        IsNegated = condition.IsNegated,
                        ScopedFilter = condition.ScopedFilter
                    });
                    break;

                case FilterGroupNode subGroup:
                    output.Groups.Add(FromFilterGroupNode(subGroup));
                    break;
            }
        }

        return output;
    }
}

/// <summary>Logical operator combining multiple filters or groups.</summary>
public enum LogicOperator
{
    /// <summary>Logical AND.</summary>
    And,

    /// <summary>Logical OR.</summary>
    Or
}
