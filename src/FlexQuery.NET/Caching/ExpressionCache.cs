using System.Linq.Expressions;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Thread-safe cache for expression trees and compiled delegates.
/// </summary>
internal static class ExpressionCache
{
    private static readonly BoundedConcurrentCache<string, LambdaExpression> _expressionCache = new();
    private static readonly BoundedConcurrentCache<string, Delegate> _compiledCache = new();

    /// <summary>
    /// Gets or creates a cached expression.
    /// </summary>
    /// <typeparam name="T">The parameter type of the expression.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to create the expression if not cached.</param>
    /// <returns>A cached or newly created expression.</returns>
    public static Expression<Func<T, bool>> GetOrAddExpression<T>(
        string key,
        Func<Expression<Func<T, bool>>> factory)
    {
        var expression = _expressionCache.GetOrAdd(key, _ => factory());
        return (Expression<Func<T, bool>>)expression;
    }

    /// <summary>
    /// Gets or creates a compiled delegate.
    /// </summary>
    /// <typeparam name="T">The parameter type of the delegate.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to create the delegate if not cached.</param>
    /// <returns>A cached or newly created compiled delegate.</returns>
    public static Func<T, bool> GetOrAddCompiled<T>(
        string key,
        Func<Func<T, bool>> factory)
    {
        var compiled = _compiledCache.GetOrAdd(key, _ => factory());

        return (Func<T, bool>)compiled!;
    }

    /// <summary>
    /// Attempts to retrieve a cached expression.
    /// </summary>
    /// <typeparam name="T">The parameter type of the expression.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="expression">When this method returns, contains the cached expression if found.</param>
    /// <returns>true if the expression was found in the cache; otherwise, false.</returns>
    public static bool TryGetExpression<T>(
        string key,
        out Expression<Func<T, bool>>? expression)
    {
        if (_expressionCache.TryGetValue(key, out var lambda))
        {
            expression = (Expression<Func<T, bool>>)lambda!;
            return true;
        }

        expression = null;
        return false;
    }

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public static void Clear()
    {
        _expressionCache.Clear();
        _compiledCache.Clear();
    }
}
