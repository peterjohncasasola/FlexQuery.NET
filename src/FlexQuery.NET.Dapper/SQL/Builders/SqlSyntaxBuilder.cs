using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Pure, stateless quoting helpers shared by the SELECT, JOIN, and WHERE builders.
/// Centralizing this avoids three slightly-different reimplementations of the same
/// alias/identifier quoting rules drifting apart over time.
/// </summary>
internal static class SqlSyntaxBuilder
{
    /// <summary>Quotes a column, prefixed with the mapping's table alias when one is set.</summary>
    public static string QuoteColumn(ISqlDialect dialect, string column, IEntityMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.TableAlias))
            return dialect.QuoteIdentifier(column);

        return $"{dialect.QuoteIdentifier(mapping.TableAlias)}.{dialect.QuoteIdentifier(column)}";
    }

    /// <summary>Quotes a fully-qualified table name, including schema when present.</summary>
    public static string QuoteTable(ISqlDialect dialect, IEntityMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.Schema))
            return dialect.QuoteIdentifier(mapping.TableName);

        return $"{dialect.QuoteIdentifier(mapping.Schema)}.{dialect.QuoteIdentifier(mapping.TableName)}";
    }

    /// <summary>
    /// Builds the ON condition for a LEFT JOIN between a parent and a related (child) table,
    /// based on the relationship's direction. Returns "1=0" for unsupported relationship types,
    /// matching the original inline behavior at every call site.
    /// </summary>
    public static string BuildJoinCondition(
        ISqlDialect dialect,
        RelationshipMapping rel,
        IEntityMapping parentMapping,
        string parentRef,
        IEntityMapping targetMapping,
        string childAlias)
    {
        return rel.RelationshipType switch
        {
            RelationshipType.OneToMany =>
                $"{dialect.QuoteIdentifier(childAlias)}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {dialect.QuoteIdentifier(parentRef)}.{dialect.QuoteIdentifier(RelationshipResolver.ResolvePrincipalColumn(parentMapping, rel))}",
            RelationshipType.ManyToOne =>
                $"{dialect.QuoteIdentifier(parentRef)}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {dialect.QuoteIdentifier(childAlias)}.{dialect.QuoteIdentifier(RelationshipResolver.ResolvePrincipalColumn(targetMapping, rel))}",
            _ => "1=0"
        };
    }
}
