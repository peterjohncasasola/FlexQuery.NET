using FlexQuery.NET.Caching;
using System.Reflection;

namespace FlexQuery.NET.Security;

internal static class SafePropertyResolver
{
    public static bool TryResolveChain(Type rootType, string fieldPath, out IReadOnlyList<PropertyInfo> chain)
        => ReflectionCache.TryResolvePropertyChain(rootType, fieldPath, out chain);

    public static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (ReflectionCache.TryGetCollectionElementType(type, out var cached))
        {
            elementType = cached!;
            return true;
        }
        elementType = null!;
        return false;
    }
}
