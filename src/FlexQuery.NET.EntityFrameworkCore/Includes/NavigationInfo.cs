using System.Reflection;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// A resolved navigation property, classified as a collection or reference navigation.
/// </summary>
/// <param name="Property">The reflected property.</param>
/// <param name="NavigationType">The property's declared type, e.g. <c>ICollection&lt;Order&gt;</c> or <c>Customer</c>.</param>
/// <param name="IsCollection">Whether the navigation is a collection.</param>
/// <param name="TargetType">The element type for collections; <see cref="NavigationType"/> for references.</param>
internal sealed record NavigationInfo(PropertyInfo Property, Type NavigationType, bool IsCollection, Type TargetType)
{
    /// <summary>
    /// The type that should appear as <c>TProperty</c> in the closed
    /// <c>Include</c> / <c>ThenInclude</c> call.
    /// </summary>
    /// <param name="allowFilteredCollection">
    /// When <c>true</c> and this navigation is a collection, returns
    /// <c>IEnumerable&lt;TargetType&gt;</c> so the selector can carry a
    /// <c>.Where(...)</c> clause. Otherwise, returns <see cref="NavigationType"/> as-is.
    /// </param>
    public Type GetPropertyTypeForSelector(bool allowFilteredCollection) =>
        IsCollection && allowFilteredCollection
            ? typeof(IEnumerable<>).MakeGenericType(TargetType)
            : NavigationType;
}