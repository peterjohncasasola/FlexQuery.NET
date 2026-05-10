namespace FlexQuery.NET.Internal;

/// <summary>
/// Internal merged runtime configuration combining global defaults with execution overrides.
/// </summary>
internal sealed class EffectiveQueryOptions
{
    /// <summary>
    /// The maximum page size that can be requested.
    /// </summary>
    public int MaxPageSize { get; init; }

    /// <summary>
    /// The default page size when none is specified.
    /// </summary>
    public int DefaultPageSize { get; init; }

    /// <summary>
    /// Whether field name matching is case-insensitive.
    /// </summary>
    public bool CaseInsensitive { get; init; }

    /// <summary>
    /// Whether to include total count in query results.
    /// </summary>
    public bool IncludeTotalCount { get; init; }

    /// <summary>
    /// Whether to throw on unauthorized field access.
    /// </summary>
    public bool StrictFieldValidation { get; init; }

    /// <summary>
    /// Maximum depth of nested field paths.
    /// </summary>
    public int MaxFieldDepth { get; init; }

    /// <summary>
    /// Whether to apply AsNoTracking to EF Core queries.
    /// </summary>
    public bool UseNoTracking { get; init; }

    /// <summary>
    /// Whether to apply AsSplitQuery to EF Core queries.
    /// </summary>
    public bool UseSplitQuery { get; init; }

    /// <summary>
    /// Whitelist of allowed fields.
    /// </summary>
    public HashSet<string>? AllowedFields { get; init; }

    /// <summary>
    /// Blacklist of blocked fields.
    /// </summary>
    public HashSet<string>? BlockedFields { get; init; }

    /// <summary>
    /// Whitelist of allowed includes.
    /// </summary>
    public HashSet<string>? AllowedIncludes { get; init; }

    /// <summary>
    /// Fields allowed specifically for filtering.
    /// </summary>
    public HashSet<string>? FilterableFields { get; init; }

    /// <summary>
    /// Fields allowed specifically for sorting.
    /// </summary>
    public HashSet<string>? SortableFields { get; init; }

    /// <summary>
    /// Fields allowed specifically for selection/projection.
    /// </summary>
    public HashSet<string>? SelectableFields { get; init; }
}