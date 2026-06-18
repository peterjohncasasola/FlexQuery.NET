using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship count queries.
/// </summary>
public class SqlCountTranslator(ISqlDialect dialect)
{
    /// <summary>
    /// Translates a count condition into a correlated COUNT subquery.
    /// </summary>
    /// <summary>Translates a count AST node into a correlated COUNT subquery fragment.</summary>
    public string Translate(
        CountExpressionNode node,
        IEntityMapping mapping,
        Func<FilterGroup, string> filterBuilder,
        Dictionary<string, object?> parameters,
        Func<string> paramNameGenerator,
        IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        var joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter)
            ? joinCondition
            : $"{joinCondition} AND ({subqueryFilter})";

        var paramName = paramNameGenerator();
        if (int.TryParse(node.Value, out var countValue))
            parameters[paramName] = countValue;
        else
            parameters[paramName] = node.Value;

        var sqlOp = FilterOperators.Normalize(node.Operator) switch
        {
            FilterOperators.Equal => "=",
            FilterOperators.NotEqual => "<>",
            FilterOperators.GreaterThan => ">",
            FilterOperators.GreaterThanOrEq => ">=",
            FilterOperators.LessThan => "<",
            FilterOperators.LessThanOrEq => "<=",
            _ => "="
        };

        return $"(SELECT COUNT(*) FROM {dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere}) {sqlOp} {paramName}";
    }

    private string BuildJoinCondition(IEntityMapping source, IEntityMapping target, Mapping.Metadata.RelationshipMapping rel, string targetAlias)
    {
        var alias = dialect.QuoteIdentifier(targetAlias);
        return rel.RelationshipType switch
        {
            Mapping.Metadata.RelationshipType.OneToMany => $"{alias}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{dialect.QuoteIdentifier(source.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            Mapping.Metadata.RelationshipType.ManyToOne => $"{dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{dialect.QuoteIdentifier(rel.ForeignKey)} = {alias}.{dialect.QuoteIdentifier(target.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            _ => "1=0"
        };
    }
}
