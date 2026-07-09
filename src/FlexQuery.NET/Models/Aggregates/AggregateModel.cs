namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Represents an aggregate projection expression (sum/count/avg).
/// </summary>
public sealed class AggregateModel
{
    /// <summary>Aggregate function.</summary>
    public AggregateFunction Function { get; set; }

    /// <summary>Field path to aggregate. Optional for count.</summary>
    public string? Field { get; set; }

    /// <summary>Output property name in the projected shape.</summary>
    public string Alias { get; set; } = string.Empty;
}