using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Resolves effective relationship metadata by combining explicit relationship
/// configuration with entity-level mapping metadata. Keeps entity and relationship
/// metadata classes independent of each other.
/// </summary>
internal static class RelationshipResolver
{
    /// <summary>
    /// Resolves the principal key column name for a relationship.
    /// </summary>
    /// <param name="principal">The principal entity mapping.</param>
    /// <param name="relationship">The relationship mapping.</param>
    /// <returns>The column name to use in the JOIN ON condition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the principal entity has composite primary keys and no explicit
    /// <see cref="RelationshipMapping.PrincipalKey"/> was configured.
    /// </exception>
    public static string ResolvePrincipalColumn(IEntityMapping principal, RelationshipMapping relationship)
    {
        if (relationship.PrincipalKey is { } explicitKey)
            return principal.GetColumnName(explicitKey);

        var keys = principal.GetKeyProperties().ToList();
        if (keys.Count > 1)
        {
            throw new InvalidOperationException(
                $"Entity '{principal.Type.Name}' has composite primary keys. " +
                $"Call HasPrincipalKey(...) on the relationship to specify which key to use.");
        }

        var propertyName = keys.FirstOrDefault();
        return propertyName != null
            ? principal.GetColumnName(propertyName)
            : "Id";
    }
}
