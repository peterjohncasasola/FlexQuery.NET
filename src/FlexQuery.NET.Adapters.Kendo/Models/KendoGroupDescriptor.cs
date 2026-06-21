using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

/// <summary>
/// Represents a Kendo UI group descriptor.
/// </summary>
public sealed class KendoGroupDescriptor
{
    /// <summary>
    /// Gets or sets the field name to group on.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the sort direction within the group ("asc" or "desc").
    /// </summary>
    [JsonPropertyName("dir")]
    public string? Dir { get; set; }

    /// <summary>
    /// Gets or sets the aggregate definitions for this group.
    /// </summary>
    [JsonPropertyName("aggregates")]
    public List<KendoAggregateDescriptor>? Aggregates { get; set; }
}
