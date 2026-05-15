namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Defines the type of relationship between two entities.
/// </summary>
public enum RelationshipType
{
    OneToOne,
    OneToMany,
    ManyToOne,
    ManyToMany
}
