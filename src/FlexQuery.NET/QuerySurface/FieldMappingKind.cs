namespace FlexQuery.NET.QuerySurface;

/// <summary>
/// Indicates how a DTO field was resolved to an entity property.
/// </summary>
public enum FieldMappingKind
{
    /// <summary>
    /// The DTO property name matches an entity property name exactly (case-insensitive)
    /// and the property types are equal.
    /// </summary>
    Convention,

    /// <summary>
    /// The mapping was explicitly registered via <see cref="BaseQueryOptions.MapField{TDto, TEntity, TProperty}"/>.
    /// </summary>
    Explicit
}
