using System.Collections.Concurrent;
using System.Linq.Expressions;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Metadata;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Optimizes projection selections by removing duplicates, merging paths,
/// and eliminating unnecessary includes.
/// </summary>
internal static class ProjectionOptimizer
{
    private static readonly ConcurrentDictionary<string, OptimizedProjection> _optimizationCache = new();

    /// <summary>
    /// Optimizes a selection tree by removing duplicates and merging navigation paths.
    /// </summary>
    /// <param name="tree">The selection node tree to optimize.</param>
    /// <param name="entityType">The root entity type.</param>
    /// <returns>An optimized projection with metadata.</returns>
    internal static OptimizedProjection Optimize(SelectionNode tree, Type entityType)
    {
        var cacheKey = GenerateOptimizationCacheKey(tree, entityType);
        return _optimizationCache.GetOrAdd(cacheKey, _ => PerformOptimization(tree, entityType));
    }

    private static OptimizedProjection PerformOptimization(SelectionNode tree, Type entityType)
    {
        var notes = new List<string>();
        var fields = new List<ProjectedField>();
        var navigationPaths = new Dictionary<string, string>();

        CollectFields(tree, entityType, "", fields, navigationPaths, level: 0, notes);

        var hasCollectionNavigation = tree.EnumerateChildren()
            .Any(c => IsCollectionNavigation(entityType, c.Key));

        return new OptimizedProjection
        {
            OptimizedTree = tree,
            Fields = fields.DistinctBy(f => f.OutputName).ToList(),
            NavigationUsage = new Dictionary<string, string>(navigationPaths),
            OptimizationNotes = notes,
            HasCollectionNavigation = hasCollectionNavigation
        };
    }

    private static void CollectFields(
        SelectionNode node,
        Type currentType,
        string currentPath,
        List<ProjectedField> fields,
        Dictionary<string, string> navPaths,
        int level,
        List<string> notes)
    {
        foreach (var kvp in node.EnumerateChildren())
        {
            var childPath = string.IsNullOrEmpty(currentPath) ? kvp.Key : $"{currentPath}.{kvp.Key}";
            var outputName = !string.IsNullOrWhiteSpace(kvp.Value.Alias) ? kvp.Value.Alias : kvp.Key;

            var propInfo = ReflectionCache.GetProperty(currentType, kvp.Key);
            if (propInfo == null) continue;

            if (Security.SafePropertyResolver.TryGetCollectionElementType(propInfo.PropertyType, out var elemType))
            {
                navPaths[outputName] = childPath;
                CollectFields(kvp.Value, elemType, childPath, fields, navPaths, level + 1, notes);
            }
            else if (!TypeClassification.IsScalarType(propInfo.PropertyType) && kvp.Value.HasChildren)
            {
                navPaths[outputName] = childPath;
                CollectFields(kvp.Value, propInfo.PropertyType, childPath, fields, navPaths, level + 1, notes);
            }
            else if (TypeClassification.IsScalarType(propInfo.PropertyType))
            {
                fields.Add(ProjectedField.Create(
                    sourcePath: childPath,
                    outputName: outputName,
                    fieldType: propInfo.PropertyType,
                    isNavigation: level > 0,
                    navigationLevel: level,
                    alias: kvp.Value.Alias));
            }
        }
    }

    private static bool IsCollectionNavigation(Type entityType, string propertyName)
    {
        var prop = ReflectionCache.GetProperty(entityType, propertyName);
        return prop != null && Security.SafePropertyResolver.TryGetCollectionElementType(prop.PropertyType, out _);
    }

    private static string GenerateOptimizationCacheKey(SelectionNode tree, Type entityType)
    {
        var parts = new List<string> { entityType.FullName ?? entityType.Name };
        foreach (var child in tree.EnumerateChildren().OrderBy(c => c.Key))
        {
            parts.Add($"{child.Key}:{(child.Value.Alias ?? "")}");
        }
        return string.Join("|", parts);
    }
}

/// <summary>
/// Represents an optimized projection result with metadata.
/// </summary>
internal sealed class OptimizedProjection
{
    internal SelectionNode OptimizedTree { get; set; } = null!;
    /// <summary>The list of projected fields after optimization.</summary>
    public IReadOnlyList<ProjectedField> Fields { get; set; } = new List<ProjectedField>();
    /// <summary>Maps navigation property aliases to their full property paths.</summary>
    public IReadOnlyDictionary<string, string> NavigationUsage { get; set; } = new Dictionary<string, string>();
    /// <summary>Notes describing optimization decisions made.</summary>
    public IReadOnlyList<string> OptimizationNotes { get; set; } = new List<string>();
    /// <summary>Whether the optimized projection includes collection navigations.</summary>
    public bool HasCollectionNavigation { get; set; }
}