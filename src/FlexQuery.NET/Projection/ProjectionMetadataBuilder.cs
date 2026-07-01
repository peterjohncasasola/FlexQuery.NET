using FlexQuery.NET.Caching;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Projection;

/// <summary>
/// Builds projection metadata by resolving field types and normalizing the selection tree.
/// </summary>
public static class ProjectionMetadataBuilder
{
    /// <summary>
    /// Builds complete projection metadata from QueryOptions.
    /// Entry point for all providers.
    /// </summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <param name="options">The query options containing select, include, and projection settings.</param>
    /// <returns>A <see cref="ProjectionMetadata"/> instance describing the projection.</returns>
    public static ProjectionMetadata Build(Type entityType, QueryOptions options)
    {
        var tree = SelectTreeBuilder.Build(options);
        var fieldTypes = ResolveFieldTypes(entityType, tree, options);
        return new ProjectionMetadata(entityType, tree, fieldTypes);
    }

    /// <summary>
    /// Resolves projected field names to CLR types from a SelectionNode.
    /// Returns empty dictionary when no projection is active.
    /// </summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <param name="selectTree">The selection node tree.</param>
    /// <param name="options">The query options for governance-aware field resolution.</param>
    /// <returns>A dictionary mapping field/output names to their CLR types.</returns>
    public static IReadOnlyDictionary<string, Type> ResolveFieldTypes(
        Type entityType,
        SelectionNode selectTree,
        QueryOptions options)
    {
        var fields = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        ResolveFieldsRecursive(entityType, selectTree, fields, options.Select, isRoot: true);
        return fields;
    }

    private static void ResolveFieldsRecursive(
        Type sourceType,
        SelectionNode node,
        Dictionary<string, Type> fields,
        IReadOnlyList<string>? governedSelectFields,
        bool isRoot)
    {
        var effective = NormalizeSelection(sourceType, node, isRoot ? governedSelectFields : null);

        foreach (var kvp in effective.EnumerateChildren())
        {
            var propName = kvp.Key;
            var childNode = kvp.Value;
            var outputName = !string.IsNullOrWhiteSpace(childNode.Alias) ? childNode.Alias : propName;

            var propInfo = ReflectionCache.GetProperty(sourceType, propName);
            if (propInfo == null) continue;

            if (ShouldBuildNestedProjection(propInfo.PropertyType, childNode))
            {
                if (IsIEnumerable(propInfo.PropertyType, out var itemType))
                {
                    ResolveFieldsRecursive(itemType, childNode, fields, null, isRoot: false);
                }
                else
                {
                    ResolveFieldsRecursive(propInfo.PropertyType, childNode, fields, null, isRoot: false);
                }
            }
            else
            {
                fields[outputName] = propInfo.PropertyType;
            }
        }
    }

    /// <summary>Normalizes a selection tree by expanding scalar wildcards and merging nodes.</summary>
    /// <param name="sourceType">The source type for property resolution.</param>
    /// <param name="selectTree">The selection node tree to normalize.</param>
    /// <param name="governedSelectFields">Optional governance-restricted field list for root-level expansion.</param>
    /// <returns>A normalized <see cref="SelectionNode"/> with all implicit scalars expanded.</returns>
    public static SelectionNode NormalizeSelection(
        Type sourceType,
        SelectionNode selectTree,
        IReadOnlyList<string>? governedSelectFields = null)
    {
        var effective = new SelectionNode();

        if (selectTree.IncludeAllScalars)
        {
            ExpandScalarFields(sourceType, effective, governedSelectFields);
        }

        foreach (var child in selectTree.EnumerateChildren())
        {
            var effectiveChild = effective.GetOrAddChild(child.Key);
            effectiveChild.Filter = child.Value.Filter;
            if (!string.IsNullOrWhiteSpace(child.Value.Alias))
                effectiveChild.Alias = child.Value.Alias;
            MergeNodes(effectiveChild, child.Value);
        }

        if (!effective.HasChildren && !selectTree.HasChildren)
        {
            ExpandScalarFields(sourceType, effective, governedSelectFields);
        }

        return effective;
    }

    private static void ExpandScalarFields(
        Type sourceType,
        SelectionNode target,
        IReadOnlyList<string>? governedSelectFields)
    {
        if (governedSelectFields is { Count: > 0 })
        {
            var rootFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in governedSelectFields)
            {
                var rootField = field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
                rootFields.Add(rootField);
            }
            foreach (var prop in ReflectionCache.GetProperties(sourceType))
            {
                if (TypeClassification.IsScalarType(prop.PropertyType) && rootFields.Contains(prop.Name))
                {
                    target.GetOrAddChild(prop.Name);
                }
            }
        }
        else
        {
            foreach (var prop in ReflectionCache.GetProperties(sourceType))
            {
                if (TypeClassification.IsScalarType(prop.PropertyType))
                {
                    target.GetOrAddChild(prop.Name);
                }
            }
        }
    }

    private static void MergeNodes(SelectionNode target, SelectionNode source)
    {
        if (source.IncludeAllScalars)
        {
            target.MarkIncludeAllScalars();
        }

        if (source.Filter != null)
        {
            target.Filter = source.Filter;
        }

        if (!string.IsNullOrWhiteSpace(source.Alias))
        {
            target.Alias = source.Alias;
        }

        foreach (var child in source.EnumerateChildren())
        {
            var targetChild = target.GetOrAddChild(child.Key);
            MergeNodes(targetChild, child.Value);
        }
    }

    internal static bool ShouldBuildNestedProjection(Type propertyType, SelectionNode node)
    {
        if (IsIEnumerable(propertyType, out _))
        {
            return node.IncludeAllScalars || node.HasChildren;
        }

        return !TypeClassification.IsScalarType(propertyType) && (node.IncludeAllScalars || node.HasChildren);
    }

    internal static bool IsIEnumerable(Type type, out Type itemType)
    {
        return SafePropertyResolver.TryGetCollectionElementType(type, out itemType);
    }
}

