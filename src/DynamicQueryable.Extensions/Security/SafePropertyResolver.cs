using System.Reflection;
using System.Text.RegularExpressions;

namespace DynamicQueryable.Security;

internal static class SafePropertyResolver
{
    private static readonly Regex SegmentPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidPathSyntax(string? fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath)) return false;

        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return false;

        return segments.All(s => SegmentPattern.IsMatch(s));
    }

    public static bool TryResolveChain(Type rootType, string fieldPath, out IReadOnlyList<PropertyInfo> chain)
    {
        chain = [];
        if (!IsValidPathSyntax(fieldPath)) return false;

        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var currentType = rootType;
        var props = new List<PropertyInfo>(segments.Length);

        foreach (var segment in segments)
        {
            var prop = currentType.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null) return false;
            props.Add(prop);

            if (TryGetCollectionElementType(prop.PropertyType, out var elementType))
            {
                currentType = elementType;
            }
            else
            {
                currentType = prop.PropertyType;
            }
        }

        chain = props;
        return true;
    }

    public static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;
        if (type == typeof(string)) return false;

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is null) return false;

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }
}
