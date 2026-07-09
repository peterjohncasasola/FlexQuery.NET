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
    /// Retained for internal use only; removed in the next major version.
    /// </summary>
    internal IDictionary<string, string>? RawParameters { get; set; }
}
