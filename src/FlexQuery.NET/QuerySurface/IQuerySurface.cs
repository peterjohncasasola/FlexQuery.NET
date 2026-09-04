using System.Reflection;

namespace FlexQuery.NET.QuerySurface;

/// <summary>
/// Provider-neutral description of the queryable surface for a given entity/response type pair.
/// QuerySurface owns all DTO-to-entity field resolution semantics.
/// It does not make governance decisions.
/// </summary>
public interface IQuerySurface
{
    /// <summary>The entity type.</summary>
    Type EntityType { get; }

    /// <summary>The response/DTO type, or null if no DTO is active.</summary>
    Type? ResponseType { get; }

    /// <summary>The comparer used for field name lookups (case-insensitive).</summary>
    StringComparer FieldNameComparer { get; }

    /// <summary>
    /// Attempts to resolve a public field name to its underlying entity mapping.
    /// </summary>
    /// <param name="fieldName">The field name to resolve (case-insensitive).</param>
    /// <param name="field">The resolved field, if successful.</param>
    /// <returns>True if the field resolves to a known entity mapping; otherwise false.</returns>
    bool TryResolve(string fieldName, out ResolvedField field);

    /// <summary>
    /// Attempts to resolve an entity property name back to its public surface field.
    /// Used to restore public field identity after name translation (e.g. grouped results).
    /// </summary>
    bool TryResolveByEntityName(string entityPropertyName, out ResolvedField field);

    /// <summary>
    /// Returns the DTO field names that are valid for default projection.
    /// When ResponseType is set, only resolvable DTO scalar properties are returned.
    /// When ResponseType is null, entity scalar property names are returned.
    /// </summary>
    IReadOnlyList<string> GetDefaultSelectFields();

    /// <summary>Returns all resolvable fields.</summary>
    IReadOnlyList<ResolvedField> GetResolvableFields();

    /// <summary>Returns the scalar properties of the response type, or empty if no ResponseType.</summary>
    IReadOnlyList<PropertyInfo> GetResponseScalarProperties();

    /// <summary>Returns the scalar properties of the entity type.</summary>
    IReadOnlyList<PropertyInfo> GetEntityScalarProperties();
}
