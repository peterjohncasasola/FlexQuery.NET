using System.Reflection;
using System.Linq.Expressions;

namespace FlexQuery.NET.QuerySurface;

/// <summary>
/// Provider-agnostic immutable field descriptor: the single resolved mapping that every
/// query operation (select, filter, sort, group, aggregate, projection, governance)
/// receives from <see cref="IQuerySurface.TryResolve"/>. The public name is the external
/// contract; the entity expression is internal execution metadata.
/// </summary>
/// <param name="SurfaceName">The public field name (DTO property name when ResponseType is set; entity property name otherwise).</param>
/// <param name="ResponseProperty">The DTO property info, or null when no ResponseType is active.</param>
/// <param name="EntityProperty">The entity property that this field maps to.</param>
/// <param name="EntityExpression">A lambda expression representing the entity-level value access.</param>
/// <param name="MappingKind">How this mapping was resolved.</param>
/// <param name="IsNavigation">True if the entity property is a navigation (non-scalar) property.</param>
/// <param name="IsImplicitMapping">True when resolved by the same-name convention (no explicit MapField).</param>
/// <param name="PublicType">The CLR type exposed on the public surface; falls back to the entity property type.</param>
public sealed record ResolvedField(
    string SurfaceName,
    PropertyInfo? ResponseProperty,
    PropertyInfo EntityProperty,
    LambdaExpression EntityExpression,
    FieldMappingKind MappingKind,
    bool IsNavigation,
    bool IsImplicitMapping = true,
    Type? PublicType = null,
    PropertyInfo? SourceProperty = null,
    Type? SourceValueType = null)
{
    /// <summary>The CLR type exposed to consumers for this field.</summary>
    public Type PublicTypeResolved => PublicType ?? ResponseProperty?.PropertyType ?? EntityProperty.PropertyType;

    /// <summary>
    /// True when the field's entity expression is a computed expression (no direct
    /// entity property backing — e.g. ForMember with e =&gt; e.FirstName + " " + e.LastName).
    /// </summary>
    public bool IsComputed => SourceProperty is null;
}
