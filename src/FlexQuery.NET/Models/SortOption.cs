namespace FlexQuery.NET.Models;

/// <summary>
/// Specifies a sort field and direction in the query tree.
/// </summary>
public class SortNode
{
    /// <summary>The property name to sort by.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Aggregate function for collection sorting (sum, count, max, min, avg).</summary>
    public string? Aggregate { get; set; }

    /// <summary>Aggregate target field for collection sorting (e.g. total).</summary>
    public string? AggregateField { get; set; }

    /// <summary>If true, sorts descending; otherwise ascending.</summary>
    public bool Descending { get; set; }
}

/// <summary>
/// Backwards-compatible sort option alias used by older test and API code.
/// </summary>
[Obsolete("SortOption is deprecated. Use SortNode instead.")]
public sealed class SortOption : SortNode
{
}
