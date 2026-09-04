using System.Linq.Expressions;
using System.Reflection;

namespace FlexQuery.NET.Mapping;

/// <summary>
/// A single resolved member mapping inside a <see cref="ITypeMap"/>: one destination
/// property and the entity-level expression that produces its value.
/// </summary>
public sealed class PropertyMap
{
    /// <summary>The public destination member name (DTO property name).</summary>
    public string DestinationName { get; init; } = string.Empty;

    /// <summary>
    /// The entity-rooted expression producing the value (direct property access for
    /// convention/direct mappings, or a computed query-translatable expression).
    /// </summary>
    public LambdaExpression SourceExpression { get; init; } = null!;

    /// <summary>
    /// The entity property backing this mapping when the source expression is a direct
    /// property access; null for computed source expressions.
    /// </summary>
    public PropertyInfo? SourceProperty { get; init; }

    /// <summary>The destination property info on the destination type.</summary>
    public PropertyInfo? DestinationProperty { get; init; }

    /// <summary>The value type produced by the source expression.</summary>
    public Type SourceValueType { get; init; } = typeof(object);

    /// <summary>The value type consumed by the destination property.</summary>
    public Type DestinationValueType { get; init; } = typeof(object);

    /// <summary>True when resolved by the same-name convention (no explicit ForMember).</summary>
    public bool IsImplicit { get; init; }

    /// <summary>True when this member is a navigation (collection or reference).</summary>
    public bool IsNavigation { get; init; }
}
