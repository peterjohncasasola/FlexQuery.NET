namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Represents a single HAVING condition against an aggregate projection.
/// </summary>
public sealed class HavingCondition
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