namespace FlexQuery.NET.Options;

/// <summary>
/// Defines server-side execution rules, validation constraints, and security policies.
/// This model separates server-side requirements from client-side query parameters.
/// </summary>
public sealed class QueryExecutionOptions : BaseQueryOptions
{

    /// <summary>
    /// Creates a new instance with default security settings.
    /// </summary>
    public QueryExecutionOptions()
    {
        // Set default values for execution options
        IncludeTotalCount = true;
        DefaultPageSize = 20;
        UseNoTracking = true;
    }

    // --- Execution Strategies ---

    /// <summary>
    /// If true (default), string comparisons will be case-insensitive using database collation.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// If true, enables expression caching for this query.
    /// When null (default), the global setting from <see cref="Options.FlexQueryOptions"/> is used.
    /// </summary>
    public bool? EnableCache { get; set; }

    /// <summary>
    /// If true, applies .AsSplitQuery() to the EF Core query.
    /// Use this for complex include trees to avoid cartesian explosion.
    /// </summary>
    public bool UseSplitQuery { get; set; }

    /// <summary>
    /// If true, applies .AsNoTracking() to the query (default is true).
    /// </summary>
    public bool UseNoTracking { get; set; } = true;

}
