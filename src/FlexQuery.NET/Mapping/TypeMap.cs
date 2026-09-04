using System.Linq.Expressions;
using System.Reflection;

namespace FlexQuery.NET.Mapping;

/// <summary>
/// A type-level mapping between an entity source type and a DTO destination type.
/// Owns the implicit same-name members discovered at creation plus any explicit
/// <c>ForMember</c> / <c>ForNavigation</c> registrations. Explicit mappings always
/// take precedence over convention mappings.
/// </summary>
public interface ITypeMap
{
    Type SourceType { get; }

    Type DestinationType { get; }

    /// <summary>All registered member mappings (implicit + explicit).</summary>
    IReadOnlyCollection<PropertyMap> PropertyMaps { get; }

    /// <summary>
    /// Attempts to resolve a destination member by name (case-insensitive).
    /// </summary>
    bool TryResolveDestinationMember(string destinationMemberName, out PropertyMap propertyMap);
}

/// <inheritdoc />
public sealed class TypeMap : ITypeMap
{
    private readonly Dictionary<string, PropertyMap> _members =
        new(StringComparer.OrdinalIgnoreCase);

    public TypeMap(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        DiscoverImplicitMembers();
    }

    public Type SourceType { get; }

    public Type DestinationType { get; }

    public IReadOnlyCollection<PropertyMap> PropertyMaps => _members.Values;

    /// <summary>Number of registered members (infrastructure).</summary>
    internal int Count => _members.Count;

    /// <inheritdoc />
    public bool TryResolveDestinationMember(string destinationMemberName, out PropertyMap propertyMap)
        => _members.TryGetValue(destinationMemberName, out propertyMap!);

    /// <summary>
    /// Registers an explicit member mapping, replacing any implicit same-name member.
    /// </summary>
    internal void RegisterMember(PropertyMap propertyMap)
        => _members[propertyMap.DestinationName] = propertyMap;

    /// <summary>
    /// Discovers same-name convention members: scalar properties with identical types,
    /// collections with element-wise compatibility, and reference navigations with
    /// identical types.
    /// </summary>
    private void DiscoverImplicitMembers()
    {
        var sourceProps = SourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destinationProps = DestinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var dtoProp in destinationProps.Values)
        {
            var entityProp = sourceProps.FirstOrDefault(
                p => p.Name.Equals(dtoProp.Name, StringComparison.OrdinalIgnoreCase) && p.CanRead);

            if (entityProp is null)
                continue;

            var typesCompatible = dtoProp.PropertyType == entityProp.PropertyType;
            if (!typesCompatible)
            {
                // Collections resolve element-wise (e.g. List<OrderResponse> over List<Order>):
                // nested child fields project against the DTO element surface.
                var bothCollections = typeof(System.Collections.IEnumerable).IsAssignableFrom(dtoProp.PropertyType)
                    && dtoProp.PropertyType != typeof(string)
                    && typeof(System.Collections.IEnumerable).IsAssignableFrom(entityProp.PropertyType)
                    && entityProp.PropertyType != typeof(string);

                if (!bothCollections)
                    continue;
            }

            var param = Expression.Parameter(SourceType, "x");
            var lambda = Expression.Lambda(Expression.Property(param, entityProp), param);

            _members[dtoProp.Name] = new PropertyMap
            {
                DestinationName = dtoProp.Name,
                SourceExpression = lambda,
                SourceProperty = entityProp,
                DestinationProperty = dtoProp,
                SourceValueType = entityProp.PropertyType,
                DestinationValueType = dtoProp.PropertyType,
                IsImplicit = true,
                IsNavigation = !FlexQuery.NET.Metadata.TypeClassification.IsScalarType(entityProp.PropertyType)
            };
        }
    }
}
