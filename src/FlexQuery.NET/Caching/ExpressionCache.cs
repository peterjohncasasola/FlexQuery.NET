using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FlexQuery.NET.Caching;

/// <summary>
/// A thread-safe cache for compiled LINQ expressions and lambdas.
/// </summary>
public static class ExpressionCache
{
    private static readonly ConcurrentDictionary<string, LambdaExpression> _expressionCache = new();
    private static readonly ConcurrentDictionary<string, Delegate> _compiledCache = new();

    /// <summary>
    /// Retrieves a cached lambda expression or adds a new one.
    /// </summary>
    public static LambdaExpression GetOrAddExpression(string key, Func<LambdaExpression> factory)
    {
        if (_expressionCache.Count >= FlexQueryCacheSettings.MaxCacheSize)
        {
            _expressionCache.Clear(); // Simple mitigation for memory leaks
        }
        return _expressionCache.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// Retrieves a cached compiled delegate or adds a new one.
    /// </summary>
    public static Delegate GetOrAddDelegate(string key, Func<Delegate> factory)
    {
        if (_compiledCache.Count >= FlexQueryCacheSettings.MaxCacheSize)
        {
            _compiledCache.Clear();
        }
        return _compiledCache.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// Clears the entire expression and compiled delegate cache.
    /// </summary>
    public static void Clear()
    {
        _expressionCache.Clear();
        _compiledCache.Clear();
    }
}
