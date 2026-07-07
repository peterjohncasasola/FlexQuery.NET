using FlexQuery.NET.Models;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Cache for parsed <see cref="QueryOptions"/>.
/// This avoids re-parsing the same query string multiple times.
/// </summary>
internal static class ParserCache
{
    private static readonly BoundedConcurrentCache<ParsedQueryCacheKey, QueryOptions> _cache = new();

    /// <summary>
    /// Attempts to get a cached <see cref="QueryOptions"/> for the given key.
    /// Returns a CLONE of the cached options to avoid side-effects from mutations.
    /// </summary>
    /// <param name="key">The cache key identifying the parsed query.</param>
    /// <param name="options">When this method returns, contains the cloned options if found.</param>
    /// <returns>true if cached options were found for the key; otherwise, false.</returns>
    public static bool TryGet(ParsedQueryCacheKey key, out QueryOptions? options)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            options = cached!.CopyQueryOptions();
            return true;
        }

        options = null;
        return false;
    }

    /// <summary>
    /// Adds or updates a cached <see cref="QueryOptions"/>.
    /// Stores a CLONE of the options to ensure the cache remains immutable.
    /// </summary>
    /// <param name="key">The cache key for the parsed query.</param>
    /// <param name="options">The query options to cache.</param>
    public static void Set(ParsedQueryCacheKey key, QueryOptions options)
    {
        _cache.Set(key, options.CopyQueryOptions());
    }

    /// <summary>
    /// Clears the parser cache.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }
}

/// <summary>
/// Represents a unique key for a parsed query configuration.
/// </summary>
/// <param name="Filter">The raw filter expression.</param>
/// <param name="Sort">The raw sort expression.</param>
/// <param name="Select">The raw select expression.</param>
/// <param name="Include">The raw include expression.</param>
/// <param name="GroupBy">The raw group-by expression.</param>
/// <param name="Having">The raw having expression.</param>
/// <param name="Page">The page number.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="IncludeCount">Whether to include total count.</param>
/// <param name="Distinct">Whether to apply distinct.</param>
/// <param name="Mode">The projection mode.</param>
/// <param name="Cursor">The cursor token for keyset pagination.</param>
/// <param name="UseKeysetPagination">Whether keyset pagination is explicitly requested.</param>
/// <param name="RawKey">Optional raw parameter key for additional uniqueness.</param>
/// <param name="Version">The version identifier for cache invalidation.</param>
internal sealed record ParsedQueryCacheKey(
    string? Filter,
    string? Sort,
    string? Select,
    string? Include,
    string? GroupBy,
    string? Having,
    int? Page,
    int? PageSize,
    bool? IncludeCount,
    bool? Distinct,
    string? Mode,
    string? Cursor = null,
    bool UseKeysetPagination = false,
    string? RawKey = null,
    string Version = "v2"
);

