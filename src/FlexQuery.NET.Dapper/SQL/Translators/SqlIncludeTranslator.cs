using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship inclusion (LEFT JOIN).
/// </summary>
internal class SqlIncludeTranslator
{
    private readonly ISqlDialect _dialect;

    /// <summary>Creates a new include translator using the specified dialect for SQL generation.</summary>
    public SqlIncludeTranslator(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Translates an include node into a LEFT JOIN clause with optional filter.
    /// </summary>
    public string Translate(IncludeNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder, IMappingRegistry registry)
    {
        var rel = mapping.GetRelationship(node.NavigationProperty);
        if (rel == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);
        var alias = _dialect.QuoteIdentifier(rel.NavigationPropertyName);
        
        string joinCondition = rel.RelationshipType switch
        {
            RelationshipType.OneToMany => $"{alias}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(mapping.TableAlias ?? mapping.TableName)}.{_dialect.QuoteIdentifier(mapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            RelationshipType.ManyToOne => $"{_dialect.QuoteIdentifier(mapping.TableAlias ?? mapping.TableName)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {alias}.{_dialect.QuoteIdentifier(targetMapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
            _ => "1=0"
        };

        var sql = $"LEFT JOIN {_dialect.QuoteIdentifier(targetMapping.TableName)} AS {alias} ON {joinCondition}";

        if (node.Filter != null)
        {
            var filterSql = filterBuilder(node.Filter);
            if (!string.IsNullOrEmpty(filterSql))
                sql += $" AND ({filterSql})";
        }

        return sql;
    }
}
