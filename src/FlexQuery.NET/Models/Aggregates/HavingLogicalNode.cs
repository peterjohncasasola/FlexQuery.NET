using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Logical combination of HAVING expressions (AND / OR).
/// </summary>
public sealed class HavingLogicalNode : HavingNode
{
    /// <summary>Logic operator: "and" or "or".</summary>
    public LogicOperator Logic { get; set; } = LogicOperator.And;

    /// <summary>Child expressions.</summary>
    public List<HavingNode> Children { get; set; } = [];
}
