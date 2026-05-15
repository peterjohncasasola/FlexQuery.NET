using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

public class RelationshipBuilder
{
    private readonly RelationshipMapping _mapping;

    public RelationshipBuilder(RelationshipMapping mapping)
    {
        _mapping = mapping;
    }

    public RelationshipBuilder WithForeignKey(string foreignKey)
    {
        _mapping.ForeignKey = foreignKey;
        return this;
    }

    public RelationshipBuilder WithPrincipalKey(string principalKey)
    {
        _mapping.PrincipalKey = principalKey;
        return this;
    }

    public RelationshipBuilder UsingJoinTable(string joinTableName, string joinTableForeignKey, string joinTableTargetKey)
    {
        _mapping.RelationshipType = RelationshipType.ManyToMany;
        _mapping.JoinTable = joinTableName;
        _mapping.JoinTableForeignKey = joinTableForeignKey;
        _mapping.JoinTableTargetKey = joinTableTargetKey;
        return this;
    }
}
