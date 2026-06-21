using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

/// <summary>
/// Represents a Kendo UI sort descriptor.
/// </summary>
public sealed class KendoSortDescriptor
{
    /// <summary>
    /// Gets or sets the field name to sort on.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the sort direction ("asc" or "desc").
    /// </summary>
    [JsonPropertyName("dir")]
    public string? Dir { get; set; }
}
