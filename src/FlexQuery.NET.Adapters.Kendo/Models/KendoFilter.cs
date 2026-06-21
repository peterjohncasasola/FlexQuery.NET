using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

/// <summary>
/// Represents a Kendo UI filter with logical operators and nested filter descriptors.
/// </summary>
public sealed class KendoFilter
{
    /// <summary>
    /// Gets or sets the logical operator for combining filters ("and" or "or").
    /// </summary>
    [JsonPropertyName("logic")]
    public string? Logic { get; set; }

    /// <summary>
    /// Gets or sets the collection of filter descriptors.
    /// </summary>
    [JsonPropertyName("filters")]
    public List<KendoFilterDescriptor> Filters { get; set; } = [];
}
