namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Defines the type of relationship between two entities.
/// </summary>
public enum RelationshipType
{
    /// <summary>One-to-one relationship.</summary>
    OneToOne,
    /// <summary>One-to-many relationship.</summary>
    OneToMany,
    /// <summary>Many-to-one relationship.</summary>
    ManyToOne,
    /// <summary>Many-to-many relationship.</summary>
    ManyToMany
}
