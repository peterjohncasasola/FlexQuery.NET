using System.Reflection;
using FlexQuery.NET.Caching;

namespace FlexQuery.NET.Helpers;

/// <summary>
/// Utilities for resolving property chains and coercing string values.
/// </summary>
internal static class TypeHelper
{
    /// <summary>
    /// Resolves a (possibly dot-separated) property chain against a root type.
    /// Returns null if any segment is not found.
    /// </summary>
    public static PropertyInfo? ResolveProperty(Type rootType, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath)) return null;

        var segments = fieldPath.Split('.');
        Type current = rootType;
        PropertyInfo? prop = null;

        foreach (var segment in segments)
        {
            prop = ReflectionCache.GetProperty(current, segment);

            if (prop is null) return null;
            current = prop.PropertyType;
        }

        return prop;
    }

    /// <summary>
    /// Attempts to convert a string value to the target type.
    /// Returns null if conversion fails (the caller decides whether to throw).
    /// </summary>
    public static object? ConvertValue(string? rawValue, Type targetType)
    {
        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (rawValue is null)
            return null;

        if (underlying == typeof(string))
            return rawValue;

        if (underlying.IsEnum)
        {
            return Enum.TryParse(underlying, rawValue, ignoreCase: true, out var enumVal)
                ? enumVal
                : null;
        }

        try
        {
            return Convert.ChangeType(rawValue, underlying, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Checks whether a type is a numeric type.</summary>
    public static bool IsNumeric(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(byte)   || t == typeof(sbyte)  ||
               t == typeof(short)  || t == typeof(ushort) ||
               t == typeof(int)    || t == typeof(uint)   ||
               t == typeof(long)   || t == typeof(ulong)  ||
               t == typeof(float)  || t == typeof(double) ||
               t == typeof(decimal);
    }
    
    public static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;
        if (type == typeof(string)) return false;

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType
                                 && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is null) return false;

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }
    
    public static bool IsNavigationProperty(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]) || type.IsValueType || type.IsPrimitive)
            return false;

        // Nullable<T> where T is a value type is not a navigation property
        return Nullable.GetUnderlyingType(type) == null;
    }
}
