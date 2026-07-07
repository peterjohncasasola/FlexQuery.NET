using System.Linq.Expressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Caches compiled projection expressions for reuse.
/// Key design: Entity type + projection fields = cached expression.
/// </summary>
internal static class ProjectionExpressionCache
{
    private static readonly BoundedConcurrentCache<string, CachedProjection> _projectionCache = new();

    /// <summary>
    /// Gets or creates a cached projection for the specified entity type and selection.
    /// </summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <param name="selectionFields">The list of field paths to include in the projection.</param>
    /// <param name="options">The query options controlling the projection mode.</param>
    /// <param name="factory">Factory function that transforms the expression and entity type into a cached entry.</param>
    /// <returns>A cached projection entry.</returns>
    public static CachedProjection GetOrAdd(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options,
        Func<Expression, Type, CachedProjection> factory)
    {
        var cacheKey = GenerateCacheKey(entityType, selectionFields, options);

        return _projectionCache.GetOrAdd(cacheKey, _ =>
        {
            var expression = ProjectionBuilder.BuildFromSelectionFields(entityType, selectionFields, options);
            return factory(expression, entityType);
        });
    }

    /// <summary>Attempts to retrieve a cached projection.</summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <param name="selectionFields">The list of field paths in the projection.</param>
    /// <param name="options">The query options controlling the projection mode.</param>
    /// <param name="cachedProjection">When this method returns, contains the cached projection if found.</param>
    /// <returns>true if a cached projection was found; otherwise, false.</returns>
    public static bool TryGet(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options,
        out CachedProjection? cachedProjection)
    {
        var cacheKey = GenerateCacheKey(entityType, selectionFields, options);
        return _projectionCache.TryGetValue(cacheKey, out cachedProjection);
    }

    /// <summary>Clears all cached projection entries.</summary>
    public static void Clear() => _projectionCache.Clear();

    /// <summary>Gets cache statistics (entry count and hit count).</summary>
    public static (int Count, int HitCount) GetStatistics() => (_projectionCache.Count, 0);

    private static string GenerateCacheKey(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options)
    {
        var fieldsKey = string.Join(",", selectionFields.OrderBy(f => f));
        var modeKey = options.ProjectionMode.ToString();
        return $"{entityType.FullName}|{modeKey}|{fieldsKey}";
    }
}

/// <summary>
/// A cached projection entry containing the expression and metadata.
/// </summary>
internal sealed class CachedProjection
{
    /// <summary>The LINQ expression representing the projection.</summary>
    public Expression ProjectionExpression { get; init; } = null!;
    /// <summary>The CLR type of the projected result.</summary>
    public Type ResultType { get; init; } = null!;
    /// <summary>The UTC timestamp when this entry was cached.</summary>
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Number of times this cached projection has been used.</summary>
    public int UsageCount { get; internal set; }
}

