namespace DynamicQueryable.Models;

/// <summary>
/// Specifies a sort field and direction.
/// </summary>
public sealed class SortOption
{
    /// <summary>The property name to sort by.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Aggregate function for collection sorting (sum, count, max, min, avg).</summary>
    public string? Aggregate { get; set; }

    /// <summary>Aggregate target field for collection sorting (e.g. total).</summary>
    public string? AggregateField { get; set; }

    /// <summary>If true, sorts descending; otherwise ascending.</summary>
    public bool Desc
    {
        get => Descending;
        set => Descending = value;
    }

    /// <summary>If true, sorts descending; otherwise ascending.</summary>
    public bool Descending { get; set; }
}
