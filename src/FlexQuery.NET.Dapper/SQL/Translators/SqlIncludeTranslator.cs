using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translator for relationship inclusion (LEFT JOIN).
/// </summary>
public class SqlIncludeTranslator
{
    private readonly ISqlDialect _dialect;

    public SqlIncludeTranslator(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Translates an include node into a LEFT JOIN clause with optional filter.
    /// </summary>
    public string Translate(IncludeNode node, IEntityMapping mapping, Func<FlexQuery.NET.Models.FilterGroup, string> filterBuilder)
    {
        var joinInfo = mapping.GetJoinInfo(node.NavigationProperty);
        if (joinInfo == null) return string.Empty;

        var alias = _dialect.QuoteIdentifier(joinInfo.NavigationProperty);
        var sql = $"LEFT JOIN {_dialect.QuoteIdentifier(joinInfo.TableName)} AS {alias} ON {joinInfo.JoinCondition}";

        if (node.Filter != null)
        {
            var filterSql = filterBuilder(node.Filter);
            if (!string.IsNullOrEmpty(filterSql))
                sql += $" AND ({filterSql})";
        }

        return sql;
    }
}
