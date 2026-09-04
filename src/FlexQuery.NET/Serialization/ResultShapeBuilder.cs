using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Serialization;

/// <summary>
/// Builds the effective public result surface for a typed DTO query from the parsed
/// <see cref="QueryOptions.Select"/> nodes and the <see cref="IQuerySurface"/>.
/// </summary>
public static class ResultShapeBuilder
{
    /// <summary>
    /// Builds the result surface for an explicit select. Returns <c>null</c> when there is
    /// no explicit select (so default projection serialization is preserved). Nested select
    /// children (e.g. <c>select=...,Orders(OrderId,Status)</c>) become nested output fields
    /// so the shape converter emits only the selected child fields for each element.
    /// </summary>
    public static IReadOnlyList<SelectOutputField>? Build(IReadOnlyList<SelectNode>? select, IQuerySurface surface)
    {
        if (select is null || select.Count == 0) return null;

        var shape = new List<SelectOutputField>(select.Count);
        foreach (var node in select)
        {
            if (!surface.TryResolve(node.Field, out var resolved) || resolved.ResponseProperty is null)
            {
                continue;
            }

            shape.Add(new SelectOutputField
            {
                SourceName = node.Field,
                SourcePropertyName = resolved.ResponseProperty.Name,
                OutputName = ToCamelCase(node.Alias ?? node.Field),
                Children = BuildNestedChildren(node.Children, resolved.ResponseProperty.PropertyType)
            });
        }

        return shape.Count > 0 ? shape : null;
    }

    private static IReadOnlyList<SelectOutputField>? BuildNestedChildren(
        IReadOnlyList<SelectNode>? children,
        Type responseType)
    {
        if (children is not { Count: > 0 }) return null;

        if (SafePropertyResolver.TryGetCollectionElementType(responseType, out var element))
        {
            responseType = element;
        }

        var shape = new List<SelectOutputField>(children.Count);
        foreach (var child in children)
        {
            var elementProp = ReflectionCache.GetProperty(responseType, child.Field);
            if (elementProp is null) continue;

            shape.Add(new SelectOutputField
            {
                SourceName = child.Field,
                SourcePropertyName = elementProp.Name,
                OutputName = ToCamelCase(child.Alias ?? child.Field),
                Children = BuildNestedChildren(child.Children, elementProp.PropertyType)
            });
        }

        return shape.Count > 0 ? shape : null;
    }

    /// <summary>
    /// Converts a PascalCase / original casing value to camelCase.
    /// </summary>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Builds a result surface for a grouped/aggregate query from the group keys and
    /// aggregate aliases. Returns <c>null</c> when neither GroupBy nor Aggregates are present.
    /// </summary>
    public static IReadOnlyList<SelectOutputField>? BuildGroupedShape(QueryOptions queryOptions)
    {
        if ((queryOptions.GroupBy?.Count ?? 0) == 0 && queryOptions.Aggregates.Count == 0)
            return null;

        var shape = (queryOptions.GroupBy ?? [])
            .Select(GroupByBuilder.GetProjectionName)
            .Select(name => new SelectOutputField
            {
                SourceName = name, 
                SourcePropertyName = name, 
                OutputName = ToCamelCase(name)
            }).ToList();

        var selectOutputFields = queryOptions
            .Aggregates
            .Select(aggregate => new SelectOutputField
            {
                SourceName = aggregate.Alias,
                SourcePropertyName = aggregate.Alias,
                OutputName = ToCamelCase(aggregate.Alias)
            });

        shape.AddRange(selectOutputFields);

        return shape.Count > 0 ? shape : null;
    }
}
