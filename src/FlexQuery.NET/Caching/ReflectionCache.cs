using System.Collections.Concurrent;
using System.Reflection;

namespace FlexQuery.NET.Caching;

internal static class ReflectionCache
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>> _propertyCache = new(concurrencyLevel: 4, capacity: 64);
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache = new(concurrencyLevel: 4, capacity: 64);
    private static readonly ConcurrentDictionary<(Type, string), IReadOnlyList<PropertyInfo>?> _chainCache = new(concurrencyLevel: 4, capacity: 64);
    private static readonly ConcurrentDictionary<Type, Type?> _collectionElementCache = new(concurrencyLevel: 4, capacity: 64);

    public static PropertyInfo? GetProperty(Type type, string propertyName)
    {
        var typeCache = _propertyCache.GetOrAdd(type, static t => new ConcurrentDictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase));

        if (typeCache.TryGetValue(propertyName, out var cached))
            return cached;

        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        typeCache.TryAdd(propertyName, prop!);
        return prop;
    }

    public static PropertyInfo[] GetProperties(Type type)
    {
        if (_propertiesCache.TryGetValue(type, out var cached))
            return cached;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        _propertiesCache[type] = props;
        return props;
    }

    public static bool TryResolvePropertyChain(Type rootType, string path, out IReadOnlyList<PropertyInfo> chain)
    {
        var key = (rootType, path);
        if (_chainCache.TryGetValue(key, out var cached))
        {
            if (cached != null)
            {
                chain = cached;
                return true;
            }
            chain = [];
            return false;
        }

        var result = ResolveChainInternal(rootType, path);
        _chainCache[key] = result;
        if (result != null)
        {
            chain = result;
            return true;
        }
        chain = [];
        return false;
    }

    public static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
        if (_collectionElementCache.TryGetValue(type, out var cached))
        {
            elementType = cached;
            return cached != null;
        }

        if (type == typeof(string))
        {
            _collectionElementCache[type] = null;
            elementType = null;
            return false;
        }

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable != null)
        {
            elementType = enumerable.GetGenericArguments()[0];
            _collectionElementCache[type] = elementType;
            return true;
        }

        _collectionElementCache[type] = null;
        elementType = null;
        return false;
    }

    private static IReadOnlyList<PropertyInfo>? ResolveChainInternal(Type rootType, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
            return null;

        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return null;

        var currentType = rootType;
        var props = new List<PropertyInfo>(segments.Length);

        foreach (var segment in segments)
        {
            var prop = GetProperty(currentType, segment);
            if (prop == null)
                return null;

            props.Add(prop);

            if (TryGetCollectionElementType(prop.PropertyType, out var elementType) && elementType != null)
                currentType = elementType;
            else
                currentType = prop.PropertyType;
        }

        return props;
    }
}
