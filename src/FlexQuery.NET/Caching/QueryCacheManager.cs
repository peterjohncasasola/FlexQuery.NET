using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Internal manager for expression and compiled lambda caching.
/// </summary>
internal static class QueryCacheManager
{
    private static readonly BoundedConcurrentCache<string, LambdaExpression> _expressionCache = new();
    private static readonly BoundedConcurrentCache<string, Delegate> _compiledCache = new();

    /// <summary>
    /// Checks if caching should be used for a specific request.
    /// </summary>
    public static bool ShouldCache(bool? localOverride) 
        => localOverride ?? FlexQueryCacheSettings.EnableCache;

    /// <summary>
    /// Gets or adds a lambda expression to the cache.
    /// </summary>
    public static T GetOrAddExpression<T>(string key, Func<T> factory) where T : LambdaExpression
    {
        return (T)_expressionCache.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// Gets or adds a compiled delegate to the cache.
    /// </summary>
    public static TDelegate GetOrAddCompiled<TDelegate>(string key, Func<TDelegate> factory) where TDelegate : Delegate
    {
        return (TDelegate)_compiledCache.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// Clears all cached expressions.
    /// </summary>
    public static void Clear()
    {
        _expressionCache.Clear();
        _compiledCache.Clear();
    }
}
