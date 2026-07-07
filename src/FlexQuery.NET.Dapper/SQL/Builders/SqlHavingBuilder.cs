using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Converters;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Builds the HAVING clause for aggregate queries.
/// </summary>
internal static class SqlHavingBuilder
{
    public static string Build(
        ISqlDialect dialect,
        HavingCondition? having,
        IEntityMapping mapping,
        SqlParameterContext parameters)
    {
        if (having is null)
        {
            return string.Empty;
        }

        var isCountStar =
            having.Function.Equals("count", StringComparison.OrdinalIgnoreCase) &&
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
                $"{having.Function.ToUpperInvariant()}({column})";
        }

        var value = ConvertValue(
            dialect,
            having,
            mapping);

        var parameterName = parameters.Add(value);

        var sqlOperator = NormalizeOperator(having.Operator);

        return $"HAVING {aggregateExpression} {sqlOperator} {parameterName}";
    }

    private static object? ConvertValue(
        ISqlDialect dialect,
        HavingCondition having,
        IEntityMapping mapping)
    {
        var value = having.Value?.Trim('"');

        object? convertedValue;

        var isCountStar =
            having.Function.Equals("count", StringComparison.OrdinalIgnoreCase) &&
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