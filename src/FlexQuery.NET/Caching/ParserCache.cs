using FlexQuery.NET.Models;
using System.Collections.Concurrent;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Cache for parsed <see cref="QueryOptions"/>.
/// This avoids re-parsing the same query string multiple times.
/// </summary>
public static class ParserCache
{
    private static readonly ConcurrentDictionary<ParsedQueryCacheKey, QueryOptions> _cache = new();

    /// <summary>
    /// Attempts to get a cached <see cref="QueryOptions"/> for the given key.
    /// Returns a CLONE of the cached options to avoid side-effects from mutations.
    /// </summary>
    public static bool TryGet(ParsedQueryCacheKey key, out QueryOptions? options)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            options = cached.Clone();
            return true;
        }

        options = null;
        return false;
    }

    /// <summary>
    /// Adds or updates a cached <see cref="QueryOptions"/>.
    /// Stores a CLONE of the options to ensure the cache remains immutable.
    /// </summary>
    public static void Set(ParsedQueryCacheKey key, QueryOptions options)
    {
        if (_cache.Count >= FlexQueryCacheSettings.MaxCacheSize)
        {
            _cache.Clear();
        }

        _cache[key] = options.Clone();
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
public sealed record ParsedQueryCacheKey(
    string? Query,
    string? Filter,
    string? Sort,
    string? Select,
    string? Includes,
    string? GroupBy,
    string? Having,
    int? Page,
    int? PageSize,
    bool? IncludeCount,
    bool? Distinct,
    string? Mode,
    string Version = "v1"
);
