using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship count queries.
/// </summary>
public class SqlCountTranslator
{
    private readonly ISqlDialect _dialect;

    public SqlCountTranslator(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Translates a count condition into a correlated COUNT subquery.
    /// </summary>
    public string Translate(
        CountExpressionNode node,
        IEntityMapping mapping,
        Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder,
        Dictionary<string, object?> parameters,
        Func<string> paramNameGenerator,
        IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        string joinCondition = BuildJoinCondition(mapping, targetMapping, rel, targetMapping.TableName);

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter)
            ? joinCondition
            : $"{joinCondition} AND ({subqueryFilter})";

        var paramName = paramNameGenerator();
        if (int.TryParse(node.Value, out var countValue))
            parameters[paramName] = countValue;
        else
            parameters[paramName] = node.Value;

        var sqlOp = FlexQuery.NET.Constants.FilterOperators.Normalize(node.Operator) switch
        {
            FlexQuery.NET.Constants.FilterOperators.Equal => "=",
            FlexQuery.NET.Constants.FilterOperators.NotEqual => "<>",
            FlexQuery.NET.Constants.FilterOperators.GreaterThan => ">",
            FlexQuery.NET.Constants.FilterOperators.GreaterThanOrEq => ">=",
            FlexQuery.NET.Constants.FilterOperators.LessThan => "<",
            FlexQuery.NET.Constants.FilterOperators.LessThanOrEq => "<=",
            _ => "="
        };

        return $"(SELECT COUNT(*) FROM {_dialect.QuoteIdentifier(targetMapping.TableName)} WHERE {subqueryWhere}) {sqlOp} {paramName}";
    }

    private string BuildJoinCondition(IEntityMapping source, IEntityMapping target, Mapping.Metadata.RelationshipMapping rel, string targetAlias)
    {
        string alias = _dialect.QuoteIdentifier(targetAlias);
        return rel.RelationshipType switch
        {
            Mapping.Metadata.RelationshipType.OneToMany => $"{alias}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{_dialect.QuoteIdentifier(source.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            Mapping.Metadata.RelationshipType.ManyToOne => $"{_dialect.QuoteIdentifier(source.TableAlias ?? source.TableName)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {alias}.{_dialect.QuoteIdentifier(target.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            _ => "1=0"
        };
    }
}
