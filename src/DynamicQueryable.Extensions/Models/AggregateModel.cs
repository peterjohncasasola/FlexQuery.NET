namespace DynamicQueryable.Models;

/// <summary>
/// Represents an aggregate projection expression (sum/count/avg).
/// </summary>
public sealed class AggregateModel
{
    /// <summary>Aggregate function name (sum, count, avg).</summary>
    public string Function { get; set; } = string.Empty;

    /// <summary>Field path to aggregate. Optional for count.</summary>
    public string? Field { get; set; }

    /// <summary>Output property name in the projected shape.</summary>
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single HAVING condition against an aggregate projection.
/// </summary>
public sealed class HavingCondition
{
    /// <summary>Aggregate function name (sum, count, avg).</summary>
    public string Function { get; set; } = string.Empty;

    /// <summary>Aggregate field path. Optional for count.</summary>
    public string? Field { get; set; }

    /// <summary>Comparison operator (eq, gt, gte, lt, lte, neq).</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>Raw comparison value.</summary>
    public string? Value { get; set; }
}
