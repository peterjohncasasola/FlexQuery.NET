using System.Reflection;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Reflects the navigation property named by an <see cref="IncludeNode"/>
/// path and classifies it as a collection or reference navigation.
/// </summary>
internal static class IncludeNavigationResolver
{
    /// <summary>
    /// Resolves <paramref name="path"/> on <paramref name="parentType"/>.
    /// Returns <c>null</c> when no matching property exists, so callers can
    /// skip the node gracefully instead of throwing.
    /// </summary>
    public static NavigationInfo? Resolve(Type parentType, string path)
    {
        var property = parentType.GetProperty(
            path,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null) return null;

        var navigationType = property.PropertyType;
        var isCollection = TypeHelper.TryGetCollectionElementType(navigationType, out var elementType);

        return new NavigationInfo(property, navigationType, isCollection, isCollection ? elementType : navigationType);
    }
}