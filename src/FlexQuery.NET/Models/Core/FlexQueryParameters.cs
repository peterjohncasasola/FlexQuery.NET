namespace FlexQuery.NET.Models;

/// <summary>
/// The standard public DTO for FlexQuery.NET.
/// Represents user-provided query parameters (filter, sort, page, etc.).
/// Represents a query submitted through HTTP query string parameters.
/// </summary>
public sealed class FlexQueryParameters : FlexQueryBase
{
    /// <summary>
    /// Optional raw dictionary of query parameters.
    /// Used by parsers for syntax auto-detection (e.g., detecting OData $ prefix).
    /// </summary>
    public IDictionary<string, string>? RawParameters { get; set; }
}
