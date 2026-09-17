using System.Linq.Expressions;
using System.Reflection;

namespace FlexQuery.NET.Mapping;

public interface ITypeMap
{
    Type SourceType { get; }
    Type DestinationType { get; }
    IReadOnlyCollection<PropertyMap> PropertyMaps { get; }
    bool TryResolveDestinationMember(string destinationMemberName, out PropertyMap propertyMap);
}

public sealed class TypeMap : ITypeMap
{
    private readonly Dictionary<string, PropertyMap> _members =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _frozen;

    public TypeMap(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        DiscoverImplicitMembers();
    }

    public Type SourceType { get; }
    public Type DestinationType { get; }
    public IReadOnlyCollection<PropertyMap> PropertyMaps => _members.Values;
    internal int Count => _members.Count;

    public bool TryResolveDestinationMember(string destinationMemberName, out PropertyMap propertyMap)
        => _members.TryGetValue(destinationMemberName, out propertyMap!);

    internal void RegisterMember(PropertyMap propertyMap)
    {
        if (_frozen)
            throw new InvalidOperationException("FlexQuery has already executed queries; configuration must happen during startup.");

        _members[propertyMap.DestinationName] = propertyMap;
    }

    internal void Freeze() => _frozen = true;

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
