namespace FlexQuery.NET.Models;

/// <summary>
/// The standard public DTO for FlexQuery.NET.
/// Represents user-provided query parameters (filter, sort, page, etc.).
/// </summary>
public sealed class FlexQueryParameters
{
    /// <summary>The JQL-lite query string.</summary>
    public string? Query { get; set; }

    /// <summary>The filter expression (DSL or JSON).</summary>
    public string? Filter { get; set; }

    /// <summary>The sorting expression (e.g., "Name:asc,Age:desc").</summary>
    public string? Sort { get; set; }

    /// <summary>The comma-separated list of fields to select.</summary>
    public string? Select { get; set; }

    /// <summary>The comma-separated list of fields to include.</summary>
    public string? Include { get; set; }

    /// <summary>Alias for Include (backward compatibility).</summary>
    [Obsolete("Use Include instead.")]
    public string? Includes { get => Include; set => Include = value; }

    /// <summary>The comma-separated list of fields to group by.</summary>
    public string? GroupBy { get; set; }

    /// <summary>The HAVING clause for grouped queries.</summary>
    public string? Having { get; set; }

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

    /// <summary>
    /// Optional raw dictionary of query parameters.
    /// Used by parsers for syntax auto-detection (e.g., detecting OData $ prefix).
    /// </summary>
    public IDictionary<string, string>? RawParameters { get; set; }
}
