using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

/// <summary>
/// Represents a single filter descriptor in a Kendo UI filter.
/// Can be a simple filter or a nested filter group.
/// </summary>
public sealed class KendoFilterDescriptor
{
    /// <summary>
    /// Gets or sets the field name to filter on.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the filter operator (e.g., "eq", "neq", "contains", "startswith").
    /// </summary>
    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    /// <summary>
    /// Gets or sets the filter value.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    /// <summary>
    /// Gets or sets the logical operator for nested filters.
    /// </summary>
    [JsonPropertyName("logic")]
    public string? Logic { get; set; }

    /// <summary>
    /// Gets or sets the nested filter descriptors for complex filtering.
    /// </summary>
    [JsonPropertyName("filters")]
    public List<KendoFilterDescriptor>? Filters { get; set; }
}
