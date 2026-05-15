using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;

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
    public string TranslateAny(AnyExpressionNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder)
    {
        var joinInfo = mapping.GetJoinInfo(node.NavigationProperty);
        if (joinInfo == null) return string.Empty;

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? joinInfo.JoinCondition 
            : $"{joinInfo.JoinCondition} AND ({subqueryFilter})";

        // e.g. EXISTS (SELECT 1 FROM orders WHERE users.Id = orders.UserId AND orders.id = @p0)
        return $"EXISTS (SELECT 1 FROM {_dialect.QuoteIdentifier(joinInfo.TableName)} WHERE {subqueryWhere})";
    }

    /// <summary>
    /// Translates an ALL condition into a NOT EXISTS subquery.
    /// </summary>
    public string TranslateAll(AllExpressionNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder)
    {
        var joinInfo = mapping.GetJoinInfo(node.NavigationProperty);
        if (joinInfo == null) return string.Empty;

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? $"{joinInfo.JoinCondition} AND NOT (1=1)" 
            : $"{joinInfo.JoinCondition} AND NOT ({subqueryFilter})";

        // e.g. NOT EXISTS (SELECT 1 FROM orders WHERE users.Id = orders.UserId AND NOT (orders.status = @p0))
        return $"NOT EXISTS (SELECT 1 FROM {_dialect.QuoteIdentifier(joinInfo.TableName)} WHERE {subqueryWhere})";
    }
}
