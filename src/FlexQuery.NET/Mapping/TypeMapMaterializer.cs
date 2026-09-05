using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace FlexQuery.NET.Mapping;

/// <summary>
/// Materializes hydrated entity instances into DTO instances using the registered
/// <see cref="ITypeMap"/> graph. Navigation members recurse through nested registered
/// type maps — raw entity types never leak into nested DTO graphs.
/// </summary>
internal static class TypeMapMaterializer
{
    private const int MaxDepth = 8;

    /// <summary>
    /// Materializes a single entity into the type map's destination type.
    /// </summary>
    public static object Materialize(
        ITypeMap map,
        object entity,
        IQueryMappingRegistry registry,
        int depth = 0,
        string navigationPath = "",
        IReadOnlySet<string>? allowedNavigationPaths = null)
    {
        var destination = Activator.CreateInstance(map.DestinationType)!;

        foreach (var propertyMap in map.PropertyMaps)
        {
            var destProp = propertyMap.DestinationProperty;
            if (destProp is null || !destProp.CanWrite)
                continue;

            var value = GetSourceValue(map, propertyMap, entity);

            if (propertyMap.IsNavigation && depth < MaxDepth)
            {
                var entityNavName = propertyMap.SourceProperty?.Name ?? propertyMap.DestinationName;
                var childPath = navigationPath.Length == 0
                    ? entityNavName
                    : $"{navigationPath}.{entityNavName}";
                var destinationPath = navigationPath.Length == 0
                    ? propertyMap.DestinationName
                    : $"{navigationPath}.{propertyMap.DestinationName}";

                if (allowedNavigationPaths is not null
                    && !allowedNavigationPaths.Contains(childPath)
                    && !allowedNavigationPaths.Contains(destinationPath))
                {
                    continue;
                }

                value = ProjectNavigation(value, propertyMap, registry, depth, childPath, allowedNavigationPaths);
            }
            else if (propertyMap.IsNavigation)
            {
                value = null; // depth guard: never leak raw entities
            }

            if (value is not null && !destProp.PropertyType.IsInstanceOfType(value))
            {
                var targetType = Nullable.GetUnderlyingType(destProp.PropertyType) ?? destProp.PropertyType;
                try
                {
                    value = Convert.ChangeType(value, targetType);
                }
                catch (InvalidCastException)
                {
                    continue;
                }
                catch (FormatException)
                {
                    continue;
                }
            }

            destProp.SetValue(destination, value);
        }

        return destination;
    }

    /// <summary>
    /// Materializes a collection (or single reference) navigation value through the
    /// nested registered type map. When no nested map is registered the value is dropped
    /// (set to null) so raw entity graphs never leak into nested DTO graphs.
    /// </summary>
    private static object? ProjectNavigation(
        object? value,
        PropertyMap propertyMap,
        IQueryMappingRegistry registry,
        int depth,
        string navigationPath,
        IReadOnlySet<string>? allowedNavigationPaths)
    {
        var dtoElementType = TryGetElementOrSelf(propertyMap.DestinationValueType);
        var entityElementType = TryGetElementOrSelf(propertyMap.SourceValueType);

        if (dtoElementType is null || entityElementType is null)
            return null;

        if (dtoElementType == entityElementType)
        {
            // Entity-typed member: the DTO author opted into the entity shape. A real
            // entity graph passes through unchanged; a projected row shape (dynamic
            // elements emitted by the split-query projection) is re-materialized
            // element-wise into the declared entity element type so the destination
            // property can actually accept the value.
            return RematerializeEntityTyped(value, dtoElementType, depth);
        }

        var nestedMap = depth < MaxDepth ? registry.Find(entityElementType, dtoElementType) : null;
        if (nestedMap is null)
            return null; // no nested map: never leak raw entity graphs.

        // Null navigation value (reference not loaded, or a projected row shape without
        // the member): nothing to materialize — the DTO member stays null/empty.
        if (value is null)
            return null;

        if (value is not (IEnumerable enumerable and not string))
            return Materialize(nestedMap, value, registry, depth + 1, navigationPath, allowedNavigationPaths);
        
        var listType = typeof(List<>).MakeGenericType(dtoElementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        var add = listType.GetMethod("Add")!;

        foreach (var item in enumerable)
        {
            if (item is null) continue;
            add.Invoke(list, [Materialize(nestedMap, item, registry, depth + 1, navigationPath, allowedNavigationPaths)]);
        }

        return list;

    }

    private static Type? TryGetElementOrSelf(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
            return type;

        if (type.IsArray)
            return type.GetElementType();

        var genericArgs = type.GetGenericArguments();
        return genericArgs.Length == 1 ? genericArgs[0] : null;
    }

    /// <summary>
    /// Coerces an entity-typed navigation value into the declared entity element type.
    /// Elements already of that type pass through untouched (real entity graphs);
    /// projected dynamic row-shape elements are re-materialized via the same-name
    /// property convention — the same fallback contract the compiled getters use.
    /// </summary>
    private static object? RematerializeEntityTyped(object? value, Type elementType, int depth)
    {
        if (value is null)
            return null;

        if (value is IEnumerable enumerable and not string)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            var add = listType.GetMethod("Add")!;

            foreach (var item in enumerable)
            {
                if (item is null) continue;
                add.Invoke(list, [CoerceToElementType(item, elementType, depth)]);
            }

            return list;
        }

        return CoerceToElementType(value, elementType, depth);
    }

    private static object CoerceToElementType(object item, Type elementType, int depth)
    {
        if (elementType.IsInstanceOfType(item))
            return item;

        var instance = Activator.CreateInstance(elementType)!;

        foreach (var targetProp in elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!targetProp.CanWrite)
                continue;

            var sourceProp = item.GetType().GetProperty(
                targetProp.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (sourceProp is null || !sourceProp.CanRead)
                continue;

            var sourceValue = sourceProp.GetValue(item);
            if (sourceValue is null)
                continue;

            var targetType = Nullable.GetUnderlyingType(targetProp.PropertyType) ?? targetProp.PropertyType;

            // Nested entity-typed collection on the projected element: coerce elements
            // recursively (generic — any depth, any type).
            if (sourceValue is IEnumerable sourceCollection and not string
                && targetProp.PropertyType != typeof(string)
                && typeof(IEnumerable).IsAssignableFrom(targetProp.PropertyType))
            {
                var targetElementType = TryGetElementOrSelf(targetProp.PropertyType);
                if (targetElementType is not null && depth + 1 < MaxDepth)
                {
                    targetProp.SetValue(
                        instance,
                        RematerializeEntityTyped(sourceCollection, targetElementType, depth + 1));
                }

                continue;
            }

            if (targetType.IsInstanceOfType(sourceValue))
            {
                targetProp.SetValue(instance, sourceValue);
                continue;
            }

            try
            {
                targetProp.SetValue(instance, Convert.ChangeType(sourceValue, targetType));
            }
            catch (InvalidCastException)
            {
                // skip members the same-name convention cannot coerce
            }
            catch (FormatException)
            {
            }
        }

        return instance;
    }

    private static object? GetSourceValue(ITypeMap map, PropertyMap propertyMap, object entity)
    {
        var getter = CompiledGetters(map).GetOrAdd(
            propertyMap.DestinationName,
            _ => BuildGetter(map, propertyMap));

        try
        {
            return getter(entity);
        }
        catch (InvalidCastException)
        {
            // The runtime instance is not the registered source type (e.g. Dapper's
            // simple-include streaming materializes dynamic row shapes whose property
            // names match the entity). Fall back to reflection on the runtime type.
            return propertyMap.SourceProperty is { } sourceProperty
                ? entity.GetType().GetProperty(sourceProperty.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    is { } runtimeProp
                    ? runtimeProp.GetValue(entity)
                    : null
                : null;
        }
    }

    private static ConcurrentDictionary<string, Func<object, object?>> CompiledGetters(ITypeMap map)
        => _compiledGetters.GetOrAdd(map, _ => new ConcurrentDictionary<string, Func<object, object?>>());

    private static readonly ConcurrentDictionary<ITypeMap, ConcurrentDictionary<string, Func<object, object?>>> _compiledGetters = new();

    private static Func<object, object?> BuildGetter(ITypeMap map, PropertyMap propertyMap)
    {
        var objParam = Expression.Parameter(typeof(object), "o");
        var typedParam = Expression.Convert(objParam, map.SourceType);
        var invoked = Expression.Invoke(propertyMap.SourceExpression, typedParam);
        var body = Expression.Convert(invoked, typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, objParam).Compile();
    }
}
