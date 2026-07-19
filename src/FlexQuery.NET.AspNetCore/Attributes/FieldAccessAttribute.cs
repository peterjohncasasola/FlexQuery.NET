namespace FlexQuery.NET.AspNetCore.Attributes;

/// <summary>
/// Specifies field-level access permissions for a controller or action.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class FieldAccessAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the list of allowed fields (whitelist).
    /// </summary>
    public string[]? Allowed { get; set; }

    /// <summary>
    /// Gets or sets the list of blocked fields (blacklist).
    /// </summary>
    public string[]? Blocked { get; set; }

    /// <summary>
    /// Gets or sets the list of fields allowed for filtering.
    /// </summary>
    public string[]? Filterable { get; set; }

    /// <summary>
    /// Gets or sets the list of fields allowed for sorting.
    /// </summary>
    public string[]? Sortable { get; set; }

    /// <summary>
    /// Gets or sets the list of fields allowed for selection/projection.
    /// </summary>
    public string[]? Selectable { get; set; }

    /// <summary>
    /// Gets or sets the list of fields allowed for grouping.
    /// </summary>
    public string[]? Groupable { get; set; }

    /// <summary>
    /// Gets or sets the list of fields allowed for aggregation.
    /// </summary>
    public string[]? Aggregatable { get; set; }
    
    /// <summary>
    /// Gets or sets the list of fields allowed for navigation.
    /// </summary>
    public string[]? AllowedIncludes { get; set; }

    /// <summary>
    /// Gets or sets the default sort field when no sort is specified.
    /// </summary>
    public string? DefaultSortField { get; set; }

    /// <summary>
    /// Gets or sets the default sort direction.
    /// </summary>
    public string? DefaultSortDirection { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed depth for nested field paths.
    /// </summary>
    public int MaxDepth { get; set; } = -1;
}
