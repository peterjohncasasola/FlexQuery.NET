using System.Linq.Expressions;

namespace FlexQuery.NET.Mapping;

/// <summary>
/// Fluent configuration surface for a type-level mapping
/// (AutoMapper-inspired ergonomics, FlexQuery-native metadata).
/// </summary>
public interface ITypeMapExpression<TSource, TDestination>
    where TDestination : class
    where TSource : class
{
    /// <summary>
    /// Maps a destination property to a query-translatable source expression.
    /// The destination must be a direct writable property; the source may be a direct
    /// entity property or a computed query-translatable expression.
    /// The property type is inferred by the compiler.
    /// </summary>
    ITypeMapExpression<TSource, TDestination> ForMember<TProperty>(
        Expression<Func<TDestination, TProperty>> destinationMember,
        Expression<Func<TSource, TProperty>> sourceMember);

    /// <summary>
    /// Maps a destination navigation collection to an entity navigation collection.
    /// Becomes metadata for include, expand, and nested DTO projection.
    /// </summary>
    ITypeMapExpression<TSource, TDestination> ForNavigation<TDestinationCollection, TSourceCollection>(
        Expression<Func<TDestination, TDestinationCollection>> destinationMember,
        Expression<Func<TSource, TSourceCollection>> sourceMember)
        where TDestinationCollection : System.Collections.IEnumerable;
}

/// <inheritdoc />
public sealed class TypeMapExpression<TSource, TDestination> : ITypeMapExpression<TSource, TDestination>
    where TDestination : class
    where TSource : class
{
    private readonly TypeMap _typeMap;

    internal TypeMapExpression(TypeMap typeMap) => _typeMap = typeMap;

    /// <inheritdoc />
    public ITypeMapExpression<TSource, TDestination> ForMember<TProperty>(
        Expression<Func<TDestination, TProperty>> destinationMember,
        Expression<Func<TSource, TProperty>> sourceMember)
    {
        var destinationProperty = Resolvers.DirectPropertySelector.Extract(destinationMember, nameof(destinationMember));
        var sourceProperty = Resolvers.DirectPropertySelector.TryExtract(sourceMember, out var src, nameof(sourceMember))
            ? src
            : null;

        _typeMap.RegisterMember(new PropertyMap
        {
            DestinationName = destinationProperty.Name,
            SourceExpression = sourceMember,
            SourceProperty = sourceProperty,
            DestinationProperty = destinationProperty,
            SourceValueType = sourceMember.ReturnType,
            DestinationValueType = destinationProperty.PropertyType,
            IsImplicit = false,
            IsNavigation = !Metadata.TypeClassification.IsScalarType(sourceMember.ReturnType)
        });

        return this;
    }

    /// <inheritdoc />
    public ITypeMapExpression<TSource, TDestination> ForNavigation<TDestinationCollection, TSourceCollection>(
        Expression<Func<TDestination, TDestinationCollection>> destinationMember,
        Expression<Func<TSource, TSourceCollection>> sourceMember)
        where TDestinationCollection : System.Collections.IEnumerable
    {
        var destinationProperty = Resolvers.DirectPropertySelector.Extract(destinationMember, nameof(destinationMember));
        var sourceProperty = Resolvers.DirectPropertySelector.Extract(sourceMember, nameof(sourceMember));

        _typeMap.RegisterMember(new PropertyMap
        {
            DestinationName = destinationProperty.Name,
            SourceExpression = sourceMember,
            SourceProperty = sourceProperty,
            DestinationProperty = destinationProperty,
            SourceValueType = sourceProperty.PropertyType,
            DestinationValueType = destinationProperty.PropertyType,
            IsImplicit = false,
            IsNavigation = true
        });

        return this;
    }
}
