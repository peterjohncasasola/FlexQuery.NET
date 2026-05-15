using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship existence queries (Any/All).
/// </summary>
public class SqlExistsTranslator
{
    private readonly ISqlDialect _dialect;

    public SqlExistsTranslator(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Translates an ANY condition into an EXISTS subquery.
    /// </summary>
    public string TranslateAny(AnyExpressionNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder, IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        string joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? joinCondition 
            : $"{joinCondition} AND ({subqueryFilter})";

        return $"EXISTS (SELECT 1 FROM {_dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere})";
    }

    /// <summary>
    /// Translates an ALL condition into a NOT EXISTS subquery.
    /// </summary>
    public string TranslateAll(AllExpressionNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder, IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        string joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? $"{joinCondition} AND NOT (1=1)" 
            : $"{joinCondition} AND NOT ({subqueryFilter})";

        return $"NOT EXISTS (SELECT 1 FROM {_dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere})";
    }

    private string BuildJoinCondition(IEntityMapping source, IEntityMapping target, RelationshipMapping rel, string targetAlias)
    {
        string alias = _dialect.QuoteIdentifier(targetAlias);
        return rel.RelationshipType switch
        {
            RelationshipType.OneToMany => $"{alias}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{_dialect.QuoteIdentifier(source.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            RelationshipType.ManyToOne => $"{_dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {alias}.{_dialect.QuoteIdentifier(target.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            _ => "1=0"
        };
    }
}
