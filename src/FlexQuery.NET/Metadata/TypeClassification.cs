using System.Collections.Concurrent;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Metadata;

internal static class TypeClassification
{
    private static readonly ConcurrentDictionary<Type, bool> _scalarCache = new();

    public static bool IsScalarType(Type type)
        => _scalarCache.GetOrAdd(type, static t =>
        {
            var underlyingType = Nullable.GetUnderlyingType(t) ?? t;
            return underlyingType.IsPrimitive
                   || underlyingType.IsEnum
                   || underlyingType == typeof(string)
                   || underlyingType == typeof(decimal)
                   || underlyingType == typeof(DateTime)
                   || underlyingType == typeof(DateTimeOffset)
                   || underlyingType == typeof(Guid)
                   || underlyingType == typeof(TimeSpan)
                   || underlyingType == typeof(DateOnly)
                   || underlyingType == typeof(TimeOnly);
        });
    
    public static bool IsCollectionType(Type type, out Type itemType)
        => SafePropertyResolver.TryGetCollectionElementType(type, out itemType);
}