using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship existence queries (Any/All).
/// </summary>
public class SqlExistsTranslator(ISqlDialect dialect)
{
    /// <summary>
    /// Translates an ANY condition into an EXISTS subquery.
    /// </summary>
    /// <summary>Translates an AnyExpressionNode into an EXISTS subquery fragment.</summary>
    public string TranslateAny(AnyExpressionNode node, IEntityMapping mapping, Func<Models.FilterGroup, string> filterBuilder, IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        string joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? joinCondition 
            : $"{joinCondition} AND ({subqueryFilter})";

        return $"EXISTS (SELECT 1 FROM {dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere})";
    }

    /// <summary>
    /// Translates an ALL condition into a NOT EXISTS subquery.
    /// </summary>
    /// <summary>Translates an AllExpressionNode into a NOT EXISTS subquery fragment.</summary>
    public string TranslateAll(AllExpressionNode node, IEntityMapping mapping, Func<Models.FilterGroup, string> filterBuilder, IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        string joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? $"{joinCondition} AND NOT (1=1)" 
            : $"{joinCondition} AND NOT ({subqueryFilter})";

        return $"NOT EXISTS (SELECT 1 FROM {dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere})";
    }

    private string BuildJoinCondition(IEntityMapping source, IEntityMapping target, RelationshipMapping rel, string targetAlias)
    {
        string alias = dialect.QuoteIdentifier(targetAlias);
        return rel.RelationshipType switch
        {
            RelationshipType.OneToMany => $"{alias}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{dialect.QuoteIdentifier(source.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            RelationshipType.ManyToOne => $"{dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {alias}.{dialect.QuoteIdentifier(target.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            _ => "1=0"
        };
    }
}
