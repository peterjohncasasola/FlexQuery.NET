namespace FlexQuery.NET.Caching;

/// <summary>
/// Global configuration for FlexQuery.NET expression caching.
/// </summary>
public static class FlexQueryCacheSettings
{
    /// <summary>
    /// Gets or sets whether expression caching is enabled globally.
    /// Default is true.
    /// </summary>
    public static bool EnableCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of entries to keep in the cache.
    /// When reached, the oldest entries are evicted to make room for new ones.
    /// Default is 2000.
    /// </summary>
    public static int MaxCacheSize { get; set; } = 2000;
}
