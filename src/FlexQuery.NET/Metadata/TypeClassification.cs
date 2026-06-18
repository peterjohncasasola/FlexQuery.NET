using FlexQuery.NET.Security;

namespace FlexQuery.NET.Metadata;

internal static class TypeClassification
{
    public static bool IsScalarType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

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
    }
    
    public static bool IsCollectionType(Type type, out Type itemType)
        => SafePropertyResolver.TryGetCollectionElementType(type, out itemType);
}