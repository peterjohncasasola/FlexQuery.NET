using System.Text.Json.Serialization;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Represents a lightweight OData-compatible query request for the MiniOData adapter.
///
/// <para>
/// This model is the canonical request contract for the MiniOData adapter and
/// supports both HTTP GET query string parsing and HTTP POST JSON payloads.
/// </para>
///
/// <para>
/// JSON property names follow the standard OData system query option names
/// (for example <c>$filter</c> and <c>$orderby</c>) while exposing idiomatic
/// .NET property names.
/// </para>
///
/// <para>
/// This model implements a lightweight subset of the OData query language and
/// is not intended to provide full OData protocol compatibility.
/// </para>
/// </summary>
public sealed class MiniODataRequest
{
    /// <summary>
    /// Gets or sets the OData filter expression.
    /// </summary>
    [JsonPropertyName("$filter")]
    public string? Filter { get; set; }

    /// <summary>
    /// Gets or sets the OData ordering expression.
    /// </summary>
    [JsonPropertyName("$orderby")]
    public string? OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the list of fields to project.
    /// </summary>
    [JsonPropertyName("$select")]
    public string? Select { get; set; }

    /// <summary>
    /// Gets or sets the navigation properties to expand.
    /// </summary>
    [JsonPropertyName("$expand")]
    public string? Expand { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of records to return.
    /// </summary>
    [JsonPropertyName("$top")]
    public int? Top { get; set; }

    /// <summary>
    /// Gets or sets the number of records to skip before returning results.
    /// </summary>
    [JsonPropertyName("$skip")]
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the total record count should be included.
    /// </summary>
    [JsonPropertyName("$count")]
    public bool? Count { get; set; }

    /// <summary>
    /// Gets or sets the OData aggregation or transformation expression.
    /// </summary>
    [JsonPropertyName("$apply")]
    public string? Apply { get; set; }
}