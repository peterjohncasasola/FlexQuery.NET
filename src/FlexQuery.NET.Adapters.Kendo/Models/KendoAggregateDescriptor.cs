using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

/// <summary>
/// Represents a Kendo UI aggregate descriptor.
/// </summary>
public sealed class KendoAggregateDescriptor
{
    /// <summary>
    /// Gets or sets the field name to aggregate on.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the aggregate function (e.g., "sum", "average", "count", "min", "max").
    /// </summary>
    [JsonPropertyName("aggregate")]
    public string? Aggregate { get; set; }
}
