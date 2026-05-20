using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

/// <summary>
/// Fluent builder for configuring relationship mappings.
/// </summary>
public class RelationshipBuilder
{
    private readonly RelationshipMapping _mapping;

    /// <summary>Creates a new relationship builder for the given mapping.</summary>
    public RelationshipBuilder(RelationshipMapping mapping)
    {
        _mapping = mapping;
    }

    /// <summary>Sets the foreign key property or column name.</summary>
    public RelationshipBuilder WithForeignKey(string foreignKey)
    {
        _mapping.ForeignKey = foreignKey;
        return this;
    }

    /// <summary>Sets the principal key column or property name on the target entity.</summary>
    public RelationshipBuilder WithPrincipalKey(string principalKey)
    {
        _mapping.PrincipalKey = principalKey;
        return this;
    }

    /// <summary>Configures a many-to-many relationship using an explicit join table.</summary>
    public RelationshipBuilder UsingJoinTable(string joinTableName, string joinTableForeignKey, string joinTableTargetKey)
    {
        _mapping.RelationshipType = RelationshipType.ManyToMany;
        _mapping.JoinTable = joinTableName;
        _mapping.JoinTableForeignKey = joinTableForeignKey;
        _mapping.JoinTableTargetKey = joinTableTargetKey;
        return this;
    }
}
