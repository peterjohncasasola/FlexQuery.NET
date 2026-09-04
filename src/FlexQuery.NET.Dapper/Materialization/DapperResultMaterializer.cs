using System.Collections;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Exceptions;

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
        // Flat projection modes deliver leaf columns through single-query JOINs
        // (SqlSelectBuilder.BuildSelectClause) — the rows ARE the flat result, so
        // entity navigation hydration must not run: it would discard the joined leaf
        // columns and replace them with nested navigation collections. Includes here
        // exist only to satisfy the navigation-include authorization contract.
        var isFlatProjection =
            (queryOptions.ProjectionMode == ProjectionMode.Flat
             || queryOptions.ProjectionMode == ProjectionMode.FlatMixed)
            && queryOptions.HasProjection();

        if (!isFlatProjection &&
            (queryOptions.Includes?.Count > 0 ||
             queryOptions.Expand?.Count > 0))
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

    public static IReadOnlyList<TResponse> MaterializeDto<TResponse>(
        IEnumerable<dynamic> rows,
        QueryOptions queryOptions,
        IQuerySurface surface,
        IMappingRegistry registry,
        Type entityType)
        where TResponse : class
    {
        var responseType = typeof(TResponse);
        var responseProps = responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var mapping = registry.GetMapping(entityType);
        var hasExplicitSelect = queryOptions.Select is { Count: > 0 };

        var materialized = new List<TResponse>();

        foreach (var row in rows)
        {
            var instance = (TResponse)Activator.CreateInstance(responseType)!;
            var source = (IDictionary<string, object>)row;

            if (!hasExplicitSelect)
            {
                // Default projection: map every resolvable field onto its response property.
                foreach (var resolved in surface.GetResolvableFields())
                {
                    if (resolved.ResponseProperty == null) continue;

                if (!TryReadValue(source, mapping, resolved.EntityProperty.Name, alias: null, out var value))
                    continue;

                if (resolved.ResponseProperty.CanWrite)
                {
                    resolved.ResponseProperty.SetValue(instance, Coerce(value, resolved.ResponseProperty.PropertyType));
                }
                }
            }
            else
            {
                // Explicit projection: bind the column value to the source ResponseProperty on
                // TResponse. The alias is purely output/result metadata (handled by the result
                // surface) and therefore does not require a matching TResponse property.
                // node.Field holds the entity property name (rewritten from the DTO name).
                foreach (var node in queryOptions.Select!)
                {
                    var entityPropertyName = node.Field;

                    var matched = surface.GetResolvableFields()
                        .FirstOrDefault(f => f.ResponseProperty != null
                            && string.Equals(f.EntityProperty.Name, entityPropertyName, StringComparison.OrdinalIgnoreCase));
                    
                    var sourceResponseName = matched?.ResponseProperty?.Name ?? entityPropertyName;

                    if (!TryReadValue(source, mapping, entityPropertyName, node.Alias, out var value))
                        continue;

                    if (responseProps.TryGetValue(sourceResponseName, out var responseProp) && responseProp.CanWrite)
                    {
                        responseProp.SetValue(instance, Coerce(value, responseProp.PropertyType));
                    }
                }
            }

            materialized.Add(instance);
        }

        return materialized;
    }

    private static object? Coerce(object? value, Type targetType)
    {
        if (value is null) return null;
        var nonNull = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value.GetType() == nonNull) return value;

        try
        {
            return Convert.ChangeType(value, nonNull);
        }
        catch (Exception)
        {
            return value;
        }
    }

    private static bool TryReadValue(
        IDictionary<string, object> source,
        IEntityMapping mapping,
        string entityPropertyName,
        string? alias,
        out object? value)
    {
        value = null;
        var columns = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(alias)) columns.Add(alias!);

        var columnName = mapping.GetColumnName(entityPropertyName);
        if (!string.IsNullOrEmpty(columnName)) columns.Add(columnName);
        columns.Add(entityPropertyName);

        foreach (var column in columns)
        {
            if (source.TryGetValue(column, out value)) return true;
        }

        foreach (var kvp in from column in columns from kvp in source 
                 where kvp.Key.Equals(column, StringComparison.OrdinalIgnoreCase) select kvp)
        {
            value = kvp.Value;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<TResponse> MaterializeGroupedDto<TResponse>(
        IEnumerable<dynamic> rows,
        QueryOptions queryOptions,
        IQuerySurface? surface = null)
        where TResponse : class
    {
        var responseType = typeof(TResponse);
        var responseProps = responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        // Group fields were rewritten to entity property names before SQL translation.
        // Map each entity name back to its public DTO surface name so result identity
        // and TResponse validation operate on public names.
        var resultFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupField in queryOptions.GroupBy ?? [])
        {
            var publicName = surface != null && surface.TryResolveByEntityName(groupField, out var resolved)
                ? resolved.SurfaceName
                : groupField;
            resultFields.Add(GroupByBuilder.GetProjectionName(publicName));
        }
        foreach (var aggregate in queryOptions.Aggregates)
        {
            resultFields.Add(aggregate.Alias);
        }

        var missingFields = resultFields.Except(responseProps.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingFields.Count > 0)
        {
            throw new FlexQueryException(
                $"Typed response '{responseType.Name}' cannot represent grouped/aggregate result field '{missingFields[0]}'. " +
                $"Ensure {responseType.Name} has a public writable property named '{missingFields[0]}'.");
        }

        // Row keys use entity property names (SQL aliases emitted from the rewritten
        // group fields) or the aggregate alias; map both back to the public DTO name.
        var entityToPublic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupField in queryOptions.GroupBy ?? [])
        {
            var rowKey = GroupByBuilder.GetProjectionName(groupField);
            var publicName = surface != null && surface.TryResolveByEntityName(groupField, out var resolved)
                ? GroupByBuilder.GetProjectionName(resolved.SurfaceName)
                : rowKey;
            entityToPublic[rowKey] = publicName;
        }

        var materialized = new List<TResponse>();
        foreach (var row in rows)
        {
            var instance = (TResponse)Activator.CreateInstance(responseType)!;
            var source = (IDictionary<string, object>)row;

            foreach (var field in resultFields)
            {
                if (!responseProps.TryGetValue(field, out var responseProp)) continue;

                if (!TryReadGroupValue(source, field, entityToPublic, out var value)) continue;

                var targetType = responseProp.PropertyType;
                if (value != null && value.GetType() != targetType && targetType != typeof(object))
                {
                    value = Convert.ChangeType(value, targetType);
                }

                responseProp.SetValue(instance, value);
            }

            materialized.Add(instance);
        }

        return materialized;
    }

    private static bool TryReadGroupValue(
        IDictionary<string, object> source,
        string publicField,
        Dictionary<string, string> entityToPublic,
        out object? value)
    {
        value = null;

        // Direct public-name hit (aggregate alias or group key), case-insensitive —
        // SQL engines differ in alias casing (e.g. SQLite preserves 'c' for 'C').
        foreach (var kvp in source)
        {
            if (!kvp.Key.Equals(publicField, StringComparison.OrdinalIgnoreCase)) continue;
            
            value = kvp.Value;
            return true;
        }

        // Entity-name hit whose public identity matches this field.
        foreach (var kvp in entityToPublic.Where(kvp => string.Equals(kvp.Value, publicField, StringComparison.OrdinalIgnoreCase)))
        {
            if (source.TryGetValue(kvp.Key, out value)) return true;
        }

        return false;
    }
}

