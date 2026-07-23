using System.Collections;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Metadata;

namespace FlexQuery.NET.Dapper.Materialization;

/// <summary>
/// Converts raw Dapper query results into the final object shape expected by
/// FlexQuery, including entity hydration, grouped projections, and plain
/// dictionary results.
/// </summary>
internal static class DapperResultMaterializer
{
    public static IReadOnlyList<object> Materialize(
        IEnumerable<dynamic> rows,
        QueryOptions queryOptions,
        Func<IEnumerable<dynamic>, IReadOnlyList<object>> hydrateIncludes,
        CancellationToken cancellationToken,
        Func<string, string>? propertyNameTransformer = null,
        Type? entityType = null)
    {
        if (queryOptions.Includes?.Count > 0 ||
            queryOptions.Expand?.Count > 0)
        {
            return hydrateIncludes(rows);
        }

        var isGroupedOrAggregated =
            queryOptions.GroupBy?.Count > 0 ||
            queryOptions.Aggregates.Count > 0;

        if (isGroupedOrAggregated)
            return MaterializeGroupedRows(rows, cancellationToken);

        if (queryOptions.HasProjection())
        {
            var projectionMode = queryOptions.ProjectionMode;
            if (projectionMode == ProjectionMode.Flat || projectionMode == ProjectionMode.FlatMixed)
            {
                return ToPlainDictionaries(rows, propertyNameTransformer);
            }

            var selectTree = SelectTreeBuilder.Build(queryOptions);
            return rows
                .Select(row => ProjectEntity(row, selectTree, entityType: entityType))
                .ToList();
        }

        return ToPlainDictionaries(rows, propertyNameTransformer);
    }

    private static IReadOnlyList<object> ToPlainDictionaries(
        IEnumerable<dynamic> rows,
        Func<string, string>? propertyNameTransformer)
    {
        return rows
            .Select(row =>
            {
                var source = (IDictionary<string, object>)row;
                if (propertyNameTransformer is null)
                {
                    return (object)new Dictionary<string, object>(source, StringComparer.OrdinalIgnoreCase);
                }

                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in source)
                {
                    dict[propertyNameTransformer(kvp.Key)] = kvp.Value;
                }

                return dict;
            })
            .ToList();
    }

    public static object ProjectEntity(object? entity, SelectionNode selectTree, Func<string, string>? propertyNameTransformer = null, Type? entityType = null)
    {
        if (entity == null) return null!;

        var runtimeType = entity.GetType();
        var sourceType = entityType ?? runtimeType;
        var values = new Dictionary<string, (Type Type, object? Value)>(StringComparer.OrdinalIgnoreCase);

        if (selectTree.IncludeAllScalars)
        {
            foreach (var prop in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                if (!TypeClassification.IsScalarType(prop.PropertyType)) continue;

                var value = ReadValue(entity, prop.Name);
                values[prop.Name] = (value?.GetType() ?? prop.PropertyType, value);
            }
        }

        foreach (var (propName, childNode) in selectTree.EnumerateChildren())
        {
            var resolvedProp = sourceType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var clrName = resolvedProp != null ? resolvedProp.Name : propName;
            var rawName = !string.IsNullOrWhiteSpace(childNode.Alias) ? childNode.Alias : clrName;
            var value = ReadValue(entity, propName, rawName, clrName);

            if (childNode.HasChildren || childNode.IncludeAllScalars)
            {
                if (value is IEnumerable enumerable and not string)
                {
                    var itemType = ResolveCollectionElementType(resolvedProp?.PropertyType);
                    var items = (from object? item in enumerable select ProjectEntity(item, childNode, entityType: itemType ?? item?.GetType())).ToList();
                    values[rawName] = (typeof(List<object>), items);
                }
                else if (value != null)
                {
                    var nested = ProjectEntity(value, childNode, entityType: resolvedProp?.PropertyType ?? value.GetType());
                    values[rawName] = (nested.GetType(), nested);
                }
            }
            else
            {
                var valueType = value?.GetType() ?? resolvedProp?.PropertyType ?? typeof(object);
                values[rawName] = (valueType, value);
            }
        }

        return CreateProjectedObject(values);
    }

    private static object CreateProjectedObject(IReadOnlyDictionary<string, (Type Type, object? Value)> values)
    {
        if (values.Count == 0)
            return Activator.CreateInstance(DynamicTypeBuilder.GetDynamicType([]))!;

        var projectedType = DynamicTypeBuilder.GetDynamicType(
            values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Type));
        var instance = Activator.CreateInstance(projectedType)!;

        foreach (var kvp in values)
        {
            var property = projectedType.GetProperty(kvp.Key);
            if (property is { CanWrite: true })
                property.SetValue(instance, kvp.Value.Value);
        }

        return instance;
    }

    private static object? ReadValue(object entity, params string[] candidateNames)
    {
        if (entity is IDictionary<string, object> dictionary)
            return ReadDictionaryValue(dictionary, candidateNames);

        if (entity is IDictionary<string, object?> nullableDictionary)
            return ReadDictionaryValue(nullableDictionary, candidateNames);

        foreach (var candidate in candidateNames)
        {
            var prop = entity.GetType().GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
                return prop.GetValue(entity);
        }

        return null;
    }

    private static object? ReadDictionaryValue<TValue>(IDictionary<string, TValue> dictionary, string[] candidateNames)
    {
        foreach (var candidate in candidateNames)
        {
            if (dictionary.TryGetValue(candidate, out var value))
                return value;

            foreach (var kvp in dictionary)
            {
                if (kvp.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
        }

        return null;
    }

    private static Type? ResolveCollectionElementType(Type? type)
    {
        if (type == null || type == typeof(string))
            return null;

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0];
    }

    private static IReadOnlyList<object> MaterializeGroupedRows(
        IEnumerable<dynamic> rows,
        CancellationToken cancellationToken)
    {
        var dictionaries = rows
            .Select(row => (IDictionary<string, object>)row)
            .ToList();

        if (dictionaries.Count == 0)
            return [];

        var propertyTypes = dictionaries[0]
            .Keys
            .ToDictionary(
                key => key,
                _ => typeof(object),
                StringComparer.OrdinalIgnoreCase);

        var projectedType =
            DynamicTypeBuilder.GetDynamicType(propertyTypes);

        cancellationToken.ThrowIfCancellationRequested();

        return dictionaries
            .Select(row =>
            {
                var instance = Activator.CreateInstance(projectedType)!;

                foreach (var kvp in row)
                {
                    var property = projectedType.GetProperty(kvp.Key);

                    if (property is { CanWrite: true })
                    {
                        property.SetValue(instance, kvp.Value);
                    }
                }

                return instance;
            })
            .ToList();
    }
}

