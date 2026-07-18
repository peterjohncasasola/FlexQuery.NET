namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Leaf HAVING condition: aggregate-function:field:operator:value.
/// </summary>
public sealed class HavingConditionNode : HavingNode
{
    /// <summary>Aggregate function.</summary>
    public AggregateFunction Function { get; set; }

    /// <summary>Aggregate field path. Optional for count.</summary>
    public string? Field { get; set; }

    /// <summary>Comparison operator (eq, gt, gte, lt, lte, neq).</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>Raw comparison value.</summary>
    public string? Value { get; set; }
}
