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
    public string Translate(CountExpressionNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder, Dictionary<string, object?> parameters, Func<string> paramNameGenerator)
    {
        var joinInfo = mapping.GetJoinInfo(node.NavigationProperty);
        if (joinInfo == null) return string.Empty;

        var subqueryFilter = filterBuilder(node.ScopedFilter);
        var subqueryWhere = string.IsNullOrEmpty(subqueryFilter) 
            ? joinInfo.JoinCondition 
            : $"{joinInfo.JoinCondition} AND ({subqueryFilter})";

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
        
        // e.g. (SELECT COUNT(*) FROM orders WHERE users.Id = orders.UserId AND status = @p0) > @p1
        return $"(SELECT COUNT(*) FROM {_dialect.QuoteIdentifier(joinInfo.TableName)} WHERE {subqueryWhere}) {sqlOp} {paramName}";
    }
}
