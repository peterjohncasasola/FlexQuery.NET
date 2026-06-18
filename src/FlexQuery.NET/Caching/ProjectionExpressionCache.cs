using System.Collections.Concurrent;
using System.Linq.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Caching;

/// <summary>
/// Caches compiled projection expressions for reuse.
/// Key design: Entity type + projection fields = cached expression.
/// </summary>
public static class ProjectionExpressionCache
{
    private static readonly ConcurrentDictionary<string, CachedProjection> _projectionCache = new();

    /// <summary>
    /// Gets or creates a cached projection for the specified entity type and selection.
    /// </summary>
    public static CachedProjection GetOrAdd(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options,
        Func<Expression, Type, CachedProjection> factory)
    {
        var cacheKey = GenerateCacheKey(entityType, selectionFields, options);

        return _projectionCache.GetOrAdd(cacheKey, _ =>
        {
            var expression = Builders.ProjectionBuilder.BuildFromSelectionFields(entityType, selectionFields, options);
            return factory(expression, entityType);
        });
    }

    public static bool TryGet(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options,
        out CachedProjection? cachedProjection)
    {
        var cacheKey = GenerateCacheKey(entityType, selectionFields, options);
        return _projectionCache.TryGetValue(cacheKey, out cachedProjection);
    }

    public static void Clear() => _projectionCache.Clear();

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
public sealed class CachedProjection
{
    public Expression ProjectionExpression { get; init; } = null!;
    public Type ResultType { get; init; } = null!;
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
    public int UsageCount { get; set; }
}