namespace FlexQuery.NET.Caching;

/// <summary>
/// Global configuration for FlexQuery.NET expression caching.
/// </summary>
public static class FlexQueryCacheSettings
{
    /// <summary>
    /// Gets or sets whether expression caching is enabled globally.
    /// Default is false.
    /// </summary>
    public static bool EnableCache { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to cache compiled lambdas (Delegates).
    /// Compilation is expensive but uses more memory. Default is false.
    /// </summary>
    public static bool CacheCompiledLambdas { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of entries to keep in the cache.
    /// When reached, the cache will be cleared to prevent memory leaks.
    /// Default is 2000.
    /// </summary>
    public static int MaxCacheSize { get; set; } = 2000;

    /// <summary>
    /// Optional expiration duration for cache entries.
    /// Note: The current default implementation uses a simple size-based eviction,
    /// but this can be used by custom providers.
    /// </summary>
    public static TimeSpan? SlidingExpiration { get; set; }
}
