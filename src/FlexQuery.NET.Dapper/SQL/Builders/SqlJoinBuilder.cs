using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Internal;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Builds the JOIN clause: joins inferred from a deep projection tree, plus explicit
/// <c>Includes</c> and <c>FilteredIncludes</c>. Depends on <see cref="SqlWhereBuilder"/>
/// to render the boolean fragment for filtered navigations/includes, mirroring the
/// existing callback-injection pattern already used with <c>SqlIncludeTranslator</c>.
/// </summary>
internal sealed class SqlJoinBuilder(
    IMappingRegistry mappingRegistry,
    ISqlDialect dialect,
    SqlIncludeTranslator includeTranslator,
    SqlWhereBuilder whereBuilder)
{
    public string BuildJoinClause(QueryOptions options, IEntityMapping mapping, SqlParameterContext parameters, SelectionNode selectTree)
    {
        var joins = new List<string>();
        var joinedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Infer joins from deep projection tree
        TraverseJoinTree(selectTree, mapping, mapping.TableAlias, joins, joinedPaths, parameters, options.CaseInsensitive);

        // 2. Explicit Includes and Filtered Includes
        if (options.Expand != null)
        {
            foreach (var filteredInclude in options.Expand)
            {
                if (!joinedPaths.Add(filteredInclude.Path)) continue;

                var rel = mapping.GetRelationship(filteredInclude.Path);
                if (rel == null) continue;

                var node = new Ast.IncludeNode
                {
                    NavigationProperty = rel.NavigationPropertyName,
                    Filter = filteredInclude.Filter
                };

                var ci = options.CaseInsensitive;
                var sql = includeTranslator.Translate(node, mapping, filterGroup =>
                {
                    var targetMapping = mappingRegistry.GetMapping(rel.TargetType);
                    return whereBuilder.BuildFilterGroupExpression(filterGroup, targetMapping, parameters, ci);
                }, mappingRegistry);

                if (!string.IsNullOrEmpty(sql)) joins.Add(sql);
            }
        }

        // Handle regular Includes
        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                if (!joinedPaths.Add(include)) continue;

                var node = new Ast.IncludeNode { NavigationProperty = include };
                var sql = includeTranslator.Translate(node, mapping, _ => string.Empty, mappingRegistry);
                if (!string.IsNullOrEmpty(sql)) joins.Add(sql);
            }
        }

        return string.Join(" ", joins);
    }
    
    private void TraverseJoinTree(SelectionNode node, IEntityMapping currentMapping, string? parentAlias, List<string> joins, HashSet<string> joinedPaths, SqlParameterContext parameters, bool caseInsensitive = false)
    {
        foreach (var child in node.EnumerateChildren())
        {
            var rel = currentMapping.GetRelationship(child.Key);
            if (rel == null) continue;

            var childAlias = rel.NavigationPropertyName;
            var targetMapping = mappingRegistry.GetMapping(rel.TargetType);

            if (joinedPaths.Add(childAlias))
            {
                var parentRef = string.IsNullOrEmpty(parentAlias) ? currentMapping.TableName : parentAlias;
                var joinCondition = SqlSyntaxBuilder.BuildJoinCondition(dialect, rel, currentMapping, parentRef, targetMapping, childAlias);
                var sql = $"LEFT JOIN {SqlSyntaxBuilder.QuoteTable(dialect, targetMapping)} AS {dialect.QuoteIdentifier(childAlias)} ON {joinCondition}";

                if (child.Value.Filter != null)
                {
                    var filterSql = whereBuilder.BuildFilterGroupExpression(child.Value.Filter, targetMapping, parameters, caseInsensitive);
                    if (!string.IsNullOrEmpty(filterSql))
                        sql += $" AND ({filterSql})";
                }

                joins.Add(sql);
            }

            TraverseJoinTree(child.Value, targetMapping, childAlias, joins, joinedPaths, parameters, caseInsensitive);
        }
    }
}