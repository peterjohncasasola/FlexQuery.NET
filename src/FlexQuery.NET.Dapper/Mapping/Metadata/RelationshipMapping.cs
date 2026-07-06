using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for a relationship/navigation property.
/// </summary>
internal sealed class RelationshipMapping
{
    /// <summary>The reflection PropertyInfo for the navigation property.</summary>
    public PropertyInfo NavigationProperty { get; }
    /// <summary>The name of the navigation property.</summary>
    public string NavigationPropertyName => NavigationProperty.Name;
    
    /// <summary>The target entity type of the relationship.</summary>
    public Type TargetType { get; set; }
    
    /// <summary>The type of relationship between the two entities.</summary>
    public RelationshipType RelationshipType { get; set; }
    
    /// <summary>
    /// The foreign key column or property name.
    /// </summary>
    public string ForeignKey { get; set; }
    
    /// <summary>
    /// The principal key column or property name on the target/principal entity.
    /// </summary>
    public string PrincipalKey { get; set; } = "Id";

    /// <summary>
    /// For many-to-many relationships, the join table name.
    /// </summary>
    public string? JoinTable { get; set; }

    /// <summary>
    /// For many-to-many relationships, the FK to the current entity.
    /// </summary>
    public string? JoinTableForeignKey { get; set; }
    
    /// <summary>
    /// For many-to-many relationships, the FK to the target entity.
    /// </summary>
    public string? JoinTableTargetKey { get; set; }

    /// <summary>Creates a new relationship mapping for the given navigation property.</summary>
    public RelationshipMapping(PropertyInfo navigationProperty, Type targetType, RelationshipType type)
    {
        NavigationProperty = navigationProperty;
        TargetType = targetType;
        RelationshipType = type;
        ForeignKey = string.Empty; // To be resolved by conventions
    }
}
