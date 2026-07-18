using System.Text;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Converters;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Builds the HAVING clause for aggregate queries.
/// </summary>
internal static class SqlHavingBuilder
{
    public static string Build(
        ISqlDialect dialect,
        HavingNode? having,
        IEntityMapping mapping,
        SqlParameterContext parameters)
    {
        if (having is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("HAVING ");
        AppendExpression(sb, having, dialect, mapping, parameters);
        return sb.ToString();
    }

    private static void AppendExpression(StringBuilder sb, HavingNode node, ISqlDialect dialect, IEntityMapping mapping, SqlParameterContext parameters)
    {
        switch (node)
        {
            case HavingConditionNode c:
                sb.Append(BuildCondition(c, dialect, mapping, parameters));
                break;
            case HavingLogicalNode l:
            {
                var parts = new List<string>();
                foreach (var child in l.Children)
                {
                    var childSb = new StringBuilder();
                    AppendExpression(childSb, child, dialect, mapping, parameters);
                    parts.Add(childSb.ToString());
                }

                var op = l.Logic.ToKeyword();
                sb.Append('(');
                sb.Append(string.Join($" {op} ", parts));
                sb.Append(')');
                break;
            }
            case HavingGroupNode g:
            {
                sb.Append('(');
                AppendExpression(sb, g.Inner, dialect, mapping, parameters);
                sb.Append(')');
                break;
            }
        }
    }

    private static string BuildCondition(HavingConditionNode having, ISqlDialect dialect, IEntityMapping mapping, SqlParameterContext parameters)
    {
        var isCountStar =
            having.Function == AggregateFunction.Count &&
            string.IsNullOrWhiteSpace(having.Field);

        string aggregateExpression;

        if (isCountStar)
        {
            aggregateExpression = "COUNT(*)";
        }
        else
        {
            var column = SqlSyntaxBuilder.QuoteColumn(
                dialect,
                mapping.GetColumnName(having.Field!),
                mapping);

            aggregateExpression =
                $"{having.Function.ToKeyword().ToUpperInvariant()}({column})";
        }

        var value = ConvertValue(dialect, having, mapping);
        var parameterName = parameters.Add(value);
        var sqlOperator = NormalizeOperator(having.Operator);

        return $"{aggregateExpression} {sqlOperator} {parameterName}";
    }

    private static object? ConvertValue(ISqlDialect dialect, HavingConditionNode having, IEntityMapping mapping)
    {
        var value = having.Value?.Trim('"');

        object? convertedValue;

        var isCountStar =
            having.Function == AggregateFunction.Count &&
            string.IsNullOrWhiteSpace(having.Field);

        if (isCountStar || string.IsNullOrWhiteSpace(having.Field))
        {
            if (long.TryParse(
                    value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var longValue))
            {
                convertedValue = longValue;
            }
            else if (double.TryParse(
                         value,
                         System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out var doubleValue))
            {
                convertedValue = doubleValue;
            }
            else
            {
                convertedValue = value;
            }
        }
        else
        {
            convertedValue = SqlValueConverter.Convert(
                having.Field!,
                value,
                mapping);
        }

        if (dialect is SqliteDialect &&
            convertedValue is decimal decimalValue)
        {
            convertedValue = (double)decimalValue;
        }

        return convertedValue;
    }

    private static string NormalizeOperator(string op)
    {
        return op.ToLowerInvariant() switch
        {
            "eq" or "equal" or "equals" or "=" => "=",
            "neq" or "ne" or "notequal" or "<>" or "!=" => "<>",
            "gt" or "greaterthan" or ">" => ">",
            "gte" or "ge" or "greaterthanorequal" or ">=" => ">=",
            "lt" or "lessthan" or "<" => "<",
            "lte" or "le" or "lessthanorequal" or "<=" => "<=",
            _ => op
        };
    }
}
