using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Dapper.Sql.Converters;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Translates a <see cref="FilterGroup"/>/<see cref="FilterCondition"/> tree into a
/// parameterized boolean SQL expression. This is the recursive core used directly for
/// WHERE clauses, and indirectly by <c>SqlJoinBuilder</c> (filtered joins/includes) and
/// by the Any/All/Count relationship translators, all of which need to render a nested
/// filter scoped to a related entity.
/// </summary>
internal sealed class SqlWhereBuilder(
    IMappingRegistry? mappingRegistry,
    ISqlDialect dialect,
    SqlExistsTranslator existsTranslator,
    SqlCountTranslator countTranslator)
{
    private bool _caseInsensitive;

    /// <summary>
    /// Sets whether string comparisons should be case-insensitive.
    /// When enabled, string columns are wrapped with LOWER() on both sides.
    /// </summary>
    public SqlWhereBuilder WithCaseInsensitive(bool value)
    {
        _caseInsensitive = value;
        return this;
    }

    /// <summary>Builds a full "WHERE ..." clause, or an empty string if the filter is empty/null.</summary>
    public string BuildWhereClause(FilterGroup? filter, IEntityMapping mapping, SqlParameterContext parameters, bool? caseInsensitive = null)
    {
        if (filter == null) return string.Empty;

        var prev = _caseInsensitive;
        if (caseInsensitive.HasValue) _caseInsensitive = caseInsensitive.Value;

        var where = BuildFilterGroupExpression(filter, mapping, parameters);

        if (caseInsensitive.HasValue) _caseInsensitive = prev;
        return string.IsNullOrEmpty(where) ? string.Empty : $"WHERE {where}";
    }

    /// <summary>
    /// Builds the boolean expression for a filter group (without the "WHERE" keyword), recursing
    /// into nested groups. Public entry point used by callers that need a bare expression, such as
    /// join-condition fragments and the Any/All/Count translator callbacks.
    /// </summary>
    public string BuildFilterGroupExpression(FilterGroup? group, IEntityMapping mapping, SqlParameterContext parameters, bool? caseInsensitive = null)
    {
        if (group == null) return string.Empty;

        var prev = _caseInsensitive;
        if (caseInsensitive.HasValue) _caseInsensitive = caseInsensitive.Value;

        var parts = new List<string>();

        foreach (var filter in group.Filters)
        {
            var expr = BuildConditionExpression(filter, mapping, parameters);
            if (!string.IsNullOrEmpty(expr))
                parts.Add(expr);
        }

        foreach (var subGroup in group.Groups)
        {
            var expr = BuildFilterGroupExpression(subGroup, mapping, parameters);
            if (!string.IsNullOrEmpty(expr))
            {
                if (group.Logic == LogicOperator.Or || group.IsNegated)
                    parts.Add($"({expr})");
                else
                    parts.Add(expr);
            }
        }

        if (caseInsensitive.HasValue) _caseInsensitive = prev;

        if (parts.Count == 0) return string.Empty;
        var result = string.Join($" {(group.Logic == LogicOperator.And ? "AND" : "OR")} ", parts);
        if (group.Logic == LogicOperator.Or || group.IsNegated)
            return $"({result})";
        return result;
    }

    private string BuildConditionExpression(FilterCondition condition, IEntityMapping mapping, SqlParameterContext parameters)
    {
        var op = FilterOperators.Normalize(condition.Operator);

        // Handle Relationship Operators
        if (op == FilterOperators.Any && condition.ScopedFilter != null)
        {
            var node = new AnyExpressionNode { NavigationProperty = condition.Field, ScopedFilter = condition.ScopedFilter };
            return existsTranslator.TranslateAny(node, mapping, group =>
            {
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, mappingRegistry);
        }

        if (op == FilterOperators.All && condition.ScopedFilter != null)
        {
            var node = new AllExpressionNode { NavigationProperty = condition.Field, ScopedFilter = condition.ScopedFilter };
            return existsTranslator.TranslateAll(node, mapping, group =>
            {
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, mappingRegistry);
        }

        if (op == FilterOperators.Count)
        {
            if (string.IsNullOrWhiteSpace(condition.Value)) return "1=0";

            var segments = condition.Value.Split(':', 2, StringSplitOptions.TrimEntries);
            if (segments.Length != 2) return "1=0";

            var node = new CountExpressionNode
            {
                NavigationProperty = condition.Field,
                ScopedFilter = condition.ScopedFilter!,
                Operator = segments[0],
                Value = segments[1]
            };

            return countTranslator.Translate(node, mapping, group =>
            {
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, parameters.RawParameters, parameters.NextName, mappingRegistry);
        }

        var column = mapping.GetColumnName(condition.Field);
        var quotedColumn = SqlSyntaxBuilder.QuoteColumn(dialect, column, mapping);

        return op switch
        {
            FilterOperators.IsNull => $"{quotedColumn} IS NULL",
            FilterOperators.IsNotNull => $"{quotedColumn} IS NOT NULL",
            FilterOperators.In => BuildInExpression(quotedColumn, condition.Field, condition.Value, mapping, parameters),
            FilterOperators.Between => BuildBetweenExpression(quotedColumn, condition.Field, condition.Value, mapping, parameters),
            FilterOperators.Contains => BuildLikeExpression(quotedColumn, condition.Value, parameters, "%", "%"),
            FilterOperators.StartsWith => BuildLikeExpression(quotedColumn, condition.Value, parameters, "", "%"),
            FilterOperators.EndsWith => BuildLikeExpression(quotedColumn, condition.Value, parameters, "%", ""),
            _ => BuildComparisonExpression(quotedColumn, condition.Field, condition.Value, op, mapping, parameters)
        };
    }

    private static bool IsStringField(IEntityMapping mapping, string field)
    {
        var prop = mapping.Type.GetProperty(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return prop?.PropertyType == typeof(string);
    }

    private string BuildComparisonExpression(string quotedColumn, string field, string? value, string op, IEntityMapping mapping, SqlParameterContext parameters)
    {
        var paramName = parameters.Add(SqlValueConverter.Convert(field, value, mapping));
        var sqlOp = op switch
        {
            FilterOperators.Equal => "=",
            FilterOperators.NotEqual => "<>",
            FilterOperators.GreaterThan => ">",
            FilterOperators.GreaterThanOrEq => ">=",
            FilterOperators.LessThan => "<",
            FilterOperators.LessThanOrEq => "<=",
            _ => "="
        };

        if (_caseInsensitive && IsStringField(mapping, field))
        {
            return $"LOWER({quotedColumn}) {sqlOp} LOWER({paramName})";
        }

        return $"{quotedColumn} {sqlOp} {paramName}";
    }

    private string BuildInExpression(string quotedColumn, string field, string? value, IEntityMapping mapping, SqlParameterContext parameters)
    {
        if (string.IsNullOrEmpty(value)) return "1 = 1";
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        var paramNames = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            paramNames[i] = parameters.Add(SqlValueConverter.Convert(field, values[i], mapping));
        }

        if (_caseInsensitive && IsStringField(mapping, field))
        {
            return $"LOWER({quotedColumn}) IN ({string.Join(", ", paramNames.Select(p => $"LOWER({p})"))})";
        }

        return $"{quotedColumn} IN ({string.Join(", ", paramNames)})";
    }

    private string BuildBetweenExpression(string quotedColumn, string field, string? value, IEntityMapping mapping, SqlParameterContext parameters)
    {
        if (string.IsNullOrEmpty(value)) return "1 = 1";
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        if (values.Length != 2) return "1 = 1";
        var fromParam = parameters.Add(SqlValueConverter.Convert(field, values[0], mapping));
        var toParam = parameters.Add(SqlValueConverter.Convert(field, values[1], mapping));

        if (_caseInsensitive && IsStringField(mapping, field))
        {
            return $"LOWER({quotedColumn}) BETWEEN LOWER({fromParam}) AND LOWER({toParam})";
        }

        return $"{quotedColumn} BETWEEN {fromParam} AND {toParam}";
    }

    private string BuildLikeExpression(string quotedColumn, string? value, SqlParameterContext parameters, string prefix, string suffix)
    {
        var paramValue = $"{prefix}{value}{suffix}";
        var paramName = parameters.Add(paramValue);

        if (_caseInsensitive && value != null)
        {
            return $"LOWER({quotedColumn}) LIKE LOWER({paramName})";
        }

        return $"{quotedColumn} LIKE {paramName}";
    }
}