namespace FlexQuery.NET.Models;

/// <summary>
/// Data Transfer Object representing a dynamic query request from a client.
/// Separates the input binding model from the internal execution model (QueryOptions).
/// </summary>
public class QueryRequest
{
    /// <summary>The filter expression (JQL, DSL, or JSON format).</summary>
    public string? Filter { get; set; }

    /// <summary>The sorting expression (e.g., "Name:asc,Age:desc").</summary>
    public string? Sort { get; set; }

    /// <summary>Comma-separated list of fields to select/project.</summary>
    public string? Select { get; set; }

    /// <summary>Comma-separated list of navigation properties to include.</summary>
    public string? Includes { get; set; }

    /// <summary>Comma-separated list of fields to group by.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Having condition for aggregate projections.</summary>
    public string? Having { get; set; }

    /// <summary>The JQL query string (used instead of Filter if providing full JQL).</summary>
    public string? Query { get; set; }

    /// <summary>The current page number (1-indexed).</summary>
    public int? Page { get; set; }

    /// <summary>The number of items per page.</summary>
    public int? PageSize { get; set; }

    /// <summary>Whether to include the total count in the result.</summary>
    public bool? IncludeCount { get; set; }

    /// <summary>Whether to apply a distinct operation.</summary>
    public bool? Distinct { get; set; }

    /// <summary>The projection mode (e.g., "nested", "flat", "flat-mixed").</summary>
    public string? Mode { get; set; }
}
