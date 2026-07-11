namespace FlexQuery.NET.Models;

/// <summary>
/// Defines the common query options shared by FlexQuery request models,
/// such as filtering, sorting, projection, grouping, and paging.
/// </summary>
public abstract class FlexQueryBase
{
    /// <summary>The filter expression (DSL or JSON).</summary>
    public string? Filter { get; set; }

    /// <summary>The sorting expression (e.g., "Name:asc,Age:desc").</summary>
    public string? Sort { get; set; }

    /// <summary>The comma-separated list of fields to select.</summary>
    public string? Select { get; set; }

    /// <summary>The comma-separated list of fields to include.</summary>
    public string? Include { get; set; }

    /// <summary>The comma-separated list of fields to group by.</summary>
    public string? GroupBy { get; set; }

    /// <summary>The HAVING clause for grouped queries.</summary>
    public string? Having { get; set; }

    /// <summary>The aggregate expressions (e.g., "SUM(Amount) AS Total, COUNT(*)").</summary>
    public string? Aggregate { get; set; }

    /// <summary>The page number (1-indexed).</summary>
    public int? Page { get; set; }

    /// <summary>The number of items per page.</summary>
    public int? PageSize { get; set; }

    /// <summary>Whether to include the total count in the result.</summary>
    public bool? IncludeCount { get; set; }

    /// <summary>Whether to apply a DISTINCT clause.</summary>
    public bool? Distinct { get; set; }

    /// <summary>The projection mode (Flat, FlatMixed, Nested).</summary>
    public string? Mode { get; set; }

    /// <summary>Serialized cursor token for keyset pagination. Pass the NextCursorToken from a previous response.</summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Enables keyset (seek/cursor) pagination instead of offset pagination.
    /// When enabled, pagination uses WHERE predicates rather than Skip/Take,
    /// providing significantly better performance for large datasets.
    /// </summary>
    public bool UseKeysetPagination { get; set; }

    /// <summary>
    /// Internal: when true, the DSL parser preserves the original filter order
    /// instead of normalizing. Set automatically when parameters originate from
    /// a raw query string (e.g. <c>QueryOptionsParser.Parse(IEnumerable&lt;KV&gt;)</c>).
    /// </summary>
    internal bool PreserveRawOrder { get; set; }
}