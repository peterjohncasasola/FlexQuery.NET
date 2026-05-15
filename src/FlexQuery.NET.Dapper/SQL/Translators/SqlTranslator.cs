using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Security;
using System.ComponentModel;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translates Core QueryOptions into SQL commands for Dapper execution.
/// </summary>
public interface ISqlTranslator
{
    /// <summary>Translates QueryOptions into a SQL command.</summary>
    SqlCommand Translate(QueryOptions options);
}

/// <summary>
/// SQL translator implementation that generates parameterized queries from QueryOptions.
/// All parameter naming and SQL generation is delegated to the ISqlDialect abstraction.
/// </summary>
public sealed class SqlTranslator : ISqlTranslator
{
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ISqlDialect _dialect;
    private readonly SqlIncludeTranslator _includeTranslator;
    private readonly SqlExistsTranslator _existsTranslator;
    private readonly SqlCountTranslator _countTranslator;
    private int _parameterIndex;

    public SqlTranslator(IMappingRegistry mappingRegistry, ISqlDialect dialect)
    {
        _mappingRegistry = mappingRegistry;
        _dialect = dialect;
        _includeTranslator = new SqlIncludeTranslator(dialect);
        _existsTranslator = new SqlExistsTranslator(dialect);
        _countTranslator = new SqlCountTranslator(dialect);
    }

    public SqlCommand Translate(QueryOptions options)
    {
        _parameterIndex = 0;
        var parameters = new Dictionary<string, object?>();

        var entityType = options.Items.TryGetValue("EntityType", out var type) ? (Type)type : typeof(object);
        var mapping = _mappingRegistry.GetMapping(entityType);

        if (options.Includes?.Count > 0 || options.FilteredIncludes?.Count > 0)
        {
            mapping.TableAlias = mapping.TableName;
        }

        var distinctClause = options.Distinct == true ? "DISTINCT" : string.Empty;
        var selectClause = BuildSelectClause(options, mapping, distinctClause);
        var fromClause = string.IsNullOrEmpty(mapping.TableAlias)
            ? $"FROM {_dialect.QuoteIdentifier(mapping.TableName)}"
            : $"FROM {_dialect.QuoteIdentifier(mapping.TableName)} AS {_dialect.QuoteIdentifier(mapping.TableAlias)}";
        var joinClause = BuildJoinClause(options, mapping, parameters);
        var whereClause = BuildWhereClause(options.Filter, mapping, parameters);
        var groupByClause = BuildGroupByClause(options.GroupBy, mapping);
        var havingClause = BuildHavingClause(options.Having, mapping, parameters);
        var orderByClause = BuildOrderByClause(options.Sort, mapping);
        var pagingClause = BuildPagingClause(options.Paging, parameters);

        var clauses = new List<string> { selectClause, fromClause, joinClause, whereClause, groupByClause, havingClause, orderByClause, pagingClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ");

        return new SqlCommand
        {
            Sql = sql,
            Parameters = parameters
        };
    }

    private string NextParam() => _dialect.CreateParameterName($"p{_parameterIndex++}");

    private string BuildSelectClause(QueryOptions options, IEntityMapping mapping, string distinctClause)
    {
        var distinctPrefix = !string.IsNullOrEmpty(distinctClause) ? $"{distinctClause} " : string.Empty;
        var selectParts = new List<string>();
        if (options.Aggregates?.Count > 0)
        {
            if (options.GroupBy?.Count > 0)
            {
                foreach (var g in options.GroupBy)
                {
                    selectParts.Add(QuoteColumn(mapping.GetColumnName(g), mapping));
                }
            }

            foreach (var agg in options.Aggregates)
            {
                var column = mapping.GetColumnName(agg.Field ?? "*");
                var quoted = QuoteColumn(column, mapping);

                if (agg.Function.Equals("count", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(agg.Field))
                {
                    selectParts.Add($"COUNT(1) AS {_dialect.QuoteIdentifier(agg.Alias)}");
                }
                else
                {
                    selectParts.Add($"{agg.Function.ToUpperInvariant()}({quoted}) AS {_dialect.QuoteIdentifier(agg.Alias)}");
                }
            }
            return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
        }

        // 1. Add Main Entity Columns
        if (options.Select?.Count > 0)
        {
            foreach (var s in options.Select)
            {
                selectParts.Add(QuoteColumn(mapping.GetColumnName(s), mapping));
            }
        }
        else if (options.GroupBy?.Count > 0)
        {
            foreach (var g in options.GroupBy)
            {
                selectParts.Add(QuoteColumn(mapping.GetColumnName(g), mapping));
            }
        }
        else
        {
            foreach (var p in mapping.GetProperties())
            {
                selectParts.Add(QuoteColumn(mapping.GetColumnName(p), mapping));
            }
        }

        // 2. Add Included Entity Columns (Flat mapping for Dapper)
        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                var joinInfo = mapping.GetJoinInfo(include);
                if (joinInfo != null)
                {
                    var targetMapping = _mappingRegistry.GetMapping(joinInfo.TargetType);
                    var targetAlias = joinInfo.NavigationProperty; // Use mapped property name for alias
                    foreach (var prop in targetMapping.GetProperties())
                    {
                        var col = targetMapping.GetColumnName(prop);
                        // Prefix with mapped navigation property name for hydration
                        var quotedAlias = _dialect.QuoteIdentifier(targetAlias);
                        var quotedCol = _dialect.QuoteIdentifier(col);
                        var aliasForHydration = _dialect.QuoteIdentifier(joinInfo.NavigationProperty + "_" + col);
                        selectParts.Add($"{quotedAlias}.{quotedCol} AS {aliasForHydration}");
                    }
                }
            }
        }

        return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
    }

    private string BuildJoinClause(QueryOptions options, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        var joins = new List<string>();
        var joinedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Handle Filtered Includes (more specific)
        if (options.FilteredIncludes != null)
        {
            foreach (var filteredInclude in options.FilteredIncludes)
            {
                if (!joinedPaths.Add(filteredInclude.Path)) continue;

                var joinInfo = mapping.GetJoinInfo(filteredInclude.Path);
                if (joinInfo == null) continue;

                var node = new FlexQuery.NET.Dapper.Sql.Ast.IncludeNode 
                { 
                    NavigationProperty = joinInfo.NavigationProperty, 
                    Filter = filteredInclude.Filter 
                };
                
                var sql = _includeTranslator.Translate(node, mapping, filterGroup => 
                {
                    var targetMapping = _mappingRegistry.GetMapping(joinInfo.TargetType!);
                    return BuildFilterGroupExpression(filterGroup, targetMapping, parameters);
                });

                if (!string.IsNullOrEmpty(sql)) joins.Add(sql);
            }
        }

        // Handle regular Includes
        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                if (!joinedPaths.Add(include)) continue;

                var node = new FlexQuery.NET.Dapper.Sql.Ast.IncludeNode { NavigationProperty = include };
                var sql = _includeTranslator.Translate(node, mapping, _ => string.Empty);
                if (!string.IsNullOrEmpty(sql)) joins.Add(sql);
            }
        }

        return string.Join(" ", joins);
    }

    private string BuildWhereClause(FilterGroup? filter, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        if (filter == null) return string.Empty;

        var where = BuildFilterGroupExpression(filter, mapping, parameters);
        return string.IsNullOrEmpty(where) ? string.Empty : $"WHERE {where}";
    }

    private string BuildFilterGroupExpression(FilterGroup? group, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        if (group == null) return string.Empty;
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

        if (parts.Count == 0) return string.Empty;
        var result = string.Join($" {(group.Logic == LogicOperator.And ? "AND" : "OR")} ", parts);
        if (group.Logic == LogicOperator.Or || group.IsNegated)
            return $"({result})";
        return result;
    }

    private string BuildConditionExpression(FilterCondition condition, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        var op = FilterOperators.Normalize(condition.Operator);

        // Handle Relationship Operators
        if (op == FilterOperators.Any && condition.ScopedFilter != null)
        {
            var node = new AnyExpressionNode { NavigationProperty = condition.Field, ScopedFilter = condition.ScopedFilter };
            return _existsTranslator.TranslateAny(node, mapping, group => 
            {
                var joinInfo = mapping.GetJoinInfo(condition.Field);
                var targetMapping = joinInfo?.TargetType != null ? _mappingRegistry.GetMapping(joinInfo.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            });
        }
        
        if (op == FilterOperators.All && condition.ScopedFilter != null)
        {
            var node = new AllExpressionNode { NavigationProperty = condition.Field, ScopedFilter = condition.ScopedFilter };
            return _existsTranslator.TranslateAll(node, mapping, group => 
            {
                var joinInfo = mapping.GetJoinInfo(condition.Field);
                var targetMapping = joinInfo?.TargetType != null ? _mappingRegistry.GetMapping(joinInfo.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            });
        }

        if (op == FilterOperators.Count)
        {
            if (string.IsNullOrWhiteSpace(condition.Value)) return "1=0";
            
            var segments = condition.Value.Split(':', 2, StringSplitOptions.TrimEntries);
            if (segments.Length != 2) return "1=0";

            var node = new CountExpressionNode 
            { 
                NavigationProperty = condition.Field, 
                ScopedFilter = condition.ScopedFilter,
                Operator = segments[0],
                Value = segments[1]
            };
            
            return _countTranslator.Translate(node, mapping, group => 
            {
                var joinInfo = mapping.GetJoinInfo(condition.Field);
                var targetMapping = joinInfo?.TargetType != null ? _mappingRegistry.GetMapping(joinInfo.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, parameters, NextParam);
        }

        var column = mapping.GetColumnName(condition.Field);
        var quotedColumn = QuoteColumn(column, mapping);

        return op switch
        {
            FilterOperators.IsNull or "isnull" => $"{quotedColumn} IS NULL",
            FilterOperators.IsNotNull or "isnotnull" => $"{quotedColumn} IS NOT NULL",
            FilterOperators.In => BuildInExpression(quotedColumn, condition.Field, condition.Value, mapping, parameters),
            FilterOperators.Between => BuildBetweenExpression(quotedColumn, condition.Field, condition.Value, mapping, parameters),
            FilterOperators.Contains => BuildLikeExpression(quotedColumn, condition.Value, parameters, "%", "%"),
            FilterOperators.StartsWith => BuildLikeExpression(quotedColumn, condition.Value, parameters, "", "%"),
            FilterOperators.EndsWith => BuildLikeExpression(quotedColumn, condition.Value, parameters, "%", ""),
            _ => BuildComparisonExpression(quotedColumn, condition.Field, condition.Value, op, mapping, parameters)
        };
    }

    private string BuildComparisonExpression(string quotedColumn, string field, string? value, string op, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        var paramName = NextParam();
        parameters[paramName] = ConvertValue(field, value, mapping);
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
        return $"{quotedColumn} {sqlOp} {paramName}";
    }

    private string BuildInExpression(string quotedColumn, string field, string? value, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        if (string.IsNullOrEmpty(value)) return "1 = 1";
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        var paramNames = values.Select((_, i) => NextParam()).ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            parameters[paramNames[i]] = ConvertValue(field, values[i], mapping);
        }
        return $"{quotedColumn} IN ({string.Join(", ", paramNames)})";
    }

    private string BuildBetweenExpression(string quotedColumn, string field, string? value, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        if (string.IsNullOrEmpty(value)) return "1 = 1";
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        if (values.Length != 2) return "1 = 1";
        var fromParam = NextParam();
        var toParam = NextParam();
        parameters[fromParam] = ConvertValue(field, values[0], mapping);
        parameters[toParam] = ConvertValue(field, values[1], mapping);
        return $"{quotedColumn} BETWEEN {fromParam} AND {toParam}";
    }

    private object? ConvertValue(string field, string? value, IEntityMapping mapping)
    {
        if (value == null) return null;

        if (SafePropertyResolver.TryResolveChain(mapping.Type, field, out var chain) && chain.Count > 0)
        {
            var targetType = chain.Last().PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                var converter = TypeDescriptor.GetConverter(underlyingType);
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return converter.ConvertFromInvariantString(value);
                }
            }
            catch { /* fallback to original string */ }
        }

        return value;
    }

    private string BuildLikeExpression(string quotedColumn, string? value, Dictionary<string, object?> parameters, string prefix, string suffix)
    {
        var paramName = NextParam();
        parameters[paramName] = $"{prefix}{value}{suffix}";
        return $"{quotedColumn} LIKE {paramName}";
    }

    private string QuoteColumn(string column, IEntityMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.TableAlias))
            return _dialect.QuoteIdentifier(column);
            
        return $"{_dialect.QuoteIdentifier(mapping.TableAlias)}.{_dialect.QuoteIdentifier(column)}";
    }

    private string BuildGroupByClause(IReadOnlyList<string>? groupBys, IEntityMapping mapping)
    {
        if (groupBys == null || groupBys.Count == 0) return string.Empty;
        var columns = groupBys.Select(g => QuoteColumn(mapping.GetColumnName(g), mapping));
        return $"GROUP BY {string.Join(", ", columns)}";
    }

    private string BuildHavingClause(HavingCondition? having, IEntityMapping mapping, Dictionary<string, object?> parameters)
    {
        if (having == null) return string.Empty;
        var column = QuoteColumn(mapping.GetColumnName(having.Field ?? "*"), mapping);
        var paramName = NextParam();
        
        var valStr = having.Value?.ToString()?.Trim('"');
        parameters[paramName] = ConvertValue(having.Field ?? string.Empty, valStr, mapping);

        var sqlOp = having.Operator.ToLowerInvariant() switch
        {
            "eq" or "equal" or "equals" or "=" => "=",
            "neq" or "ne" or "notequal" or "<>" or "!=" => "<>",
            "gt" or "greaterthan" or ">" => ">",
            "gte" or "ge" or "greaterthanorequal" or ">=" => ">=",
            "lt" or "lessthan" or "<" => "<",
            "lte" or "le" or "lessthanorequal" or "<=" => "<=",
            _ => having.Operator
        };

        return $"HAVING {having.Function.ToUpperInvariant()}({column}) {sqlOp} {paramName}";
    }

    private string BuildOrderByClause(IReadOnlyList<SortNode>? sorts, IEntityMapping mapping)
    {
        if (sorts == null || sorts.Count == 0) return string.Empty;
        var columns = sorts.Select(s =>
        {
            var column = QuoteColumn(mapping.GetColumnName(s.Field), mapping);
            return s.Descending ? $"{column} DESC" : column;
        });
        return $"ORDER BY {string.Join(", ", columns)}";
    }

    private string BuildPagingClause(PagingOptions paging, Dictionary<string, object?> parameters)
    {
        if (paging.Disabled) return string.Empty;

        var offset = (paging.Page - 1) * paging.PageSize;
        var offsetParam = _dialect.CreateParameterName("Offset");
        var limitParam = _dialect.CreateParameterName("PageSize");
         parameters[offsetParam] = offset;
         parameters[limitParam] = paging.PageSize;

        return _dialect.GetPagingClause(offsetParam, limitParam);
     }
}
