using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Ast;
using FlexQuery.NET.Security;
using FlexQuery.NET.Metadata;
using System.ComponentModel;
using System.Reflection;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// Translates Core QueryOptions into SQL commands for Dapper execution.
/// </summary>
public interface ISqlTranslator
{
    /// <summary>Translates QueryOptions into fully parameterized SQL.</summary>
    SqlCommand Translate(QueryOptions options);

    /// <summary>Translates QueryOptions aggregates list into parameterized SQL.</summary>
    SqlCommand TranslateAggregates(QueryOptions options);
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

    /// <summary>Creates a new SQL translator.</summary>
    public SqlTranslator(IMappingRegistry mappingRegistry, ISqlDialect dialect)
    {
        _mappingRegistry = mappingRegistry;
        _dialect = dialect;
        _includeTranslator = new SqlIncludeTranslator(dialect);
        _existsTranslator = new SqlExistsTranslator(dialect);
        _countTranslator = new SqlCountTranslator(dialect);
    }

    /// <summary>Translates the query options into a complete SQL command with parameters.</summary>
    public SqlCommand Translate(QueryOptions options)
    {
        _parameterIndex = 0;
        var parameters = new Dictionary<string, object?>();

        var entityType = options.Items.TryGetValue(ContextKeys.EntityType, out var type) ? (Type)type : typeof(object);
        var mapping = _mappingRegistry.GetMapping(entityType);
        var selectTree = Helpers.SelectTreeBuilder.Build(options);

        if (options.Includes?.Count > 0 || options.FilteredIncludes?.Count > 0 || selectTree.HasChildren)
        {
            mapping.TableAlias = mapping.TableName;
        }

        var distinctClause = options.Distinct == true ? "DISTINCT" : string.Empty;

        string selectClause;
        string joinClause;
        List<string>? flatJoins = null;

        if (options.ProjectionMode is ProjectionMode.Flat or ProjectionMode.FlatMixed)
        {
            (selectClause, joinClause, flatJoins) = BuildFlatSelectClause(options, mapping, distinctClause, selectTree);
        }
        else
        {
            selectClause = BuildSelectClause(options, mapping, distinctClause, selectTree);
            joinClause = BuildJoinClause(options, mapping, parameters, selectTree);
        }

        var fromClause = string.IsNullOrEmpty(mapping.TableAlias)
            ? $"FROM {_dialect.QuoteIdentifier(mapping.TableName)}"
            : $"FROM {_dialect.QuoteIdentifier(mapping.TableName)} AS {_dialect.QuoteIdentifier(mapping.TableAlias)}";
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
            Parameters = parameters,
            FlatJoins = flatJoins
        };
    }

    /// <summary>Translates aggregates list into parameterized SQL.</summary>
    public SqlCommand TranslateAggregates(QueryOptions options)
    {
        _parameterIndex = 0;
        var parameters = new Dictionary<string, object?>();

        var entityType = options.Items.TryGetValue(ContextKeys.EntityType, out var type) ? (Type)type : typeof(object);
        var mapping = _mappingRegistry.GetMapping(entityType);
        var selectTree = Helpers.SelectTreeBuilder.Build(options);

        if (options.Includes?.Count > 0 || options.FilteredIncludes?.Count > 0 || selectTree.HasChildren)
        {
            mapping.TableAlias = mapping.TableName;
        }

        var selectParts = new List<string>();
        foreach (var agg in options.Aggregates)
        {
            var column = mapping.GetColumnName(agg.Field ?? "*");
            var quoted = QuoteColumn(column, mapping);

            if (agg.Function.Equals("count", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(agg.Field) || agg.Field == "*"))
            {
                selectParts.Add($"COUNT(1) AS {_dialect.QuoteIdentifier(agg.Alias)}");
            }
            else
            {
                selectParts.Add($"{agg.Function.ToUpperInvariant()}({quoted}) AS {_dialect.QuoteIdentifier(agg.Alias)}");
            }
        }
        var selectClause = $"SELECT {string.Join(", ", selectParts)}";

        var fromClause = string.IsNullOrEmpty(mapping.TableAlias)
            ? $"FROM {_dialect.QuoteIdentifier(mapping.TableName)}"
            : $"FROM {_dialect.QuoteIdentifier(mapping.TableName)} AS {_dialect.QuoteIdentifier(mapping.TableAlias)}";
        var joinClause = BuildJoinClause(options, mapping, parameters, selectTree);
        var whereClause = BuildWhereClause(options.Filter, mapping, parameters);

        var clauses = new List<string> { selectClause, fromClause, joinClause, whereClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ");

        return new SqlCommand
        {
            Sql = sql,
            Parameters = parameters
        };
    }

    private string NextParam() => _dialect.CreateParameterName($"p{_parameterIndex++}");

    /// <summary>Builds the SELECT clause by recursively walking the SelectionNode AST.</summary>
    private string BuildSelectClause(QueryOptions options, IEntityMapping mapping, string distinctClause, SelectionNode selectTree)
    {
        var distinctPrefix = !string.IsNullOrEmpty(distinctClause) ? $"{distinctClause} " : string.Empty;
        var selectParts = new List<string>();
        if (options.Aggregates?.Count > 0 && options.GroupBy?.Count > 0)
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

                if (agg.Function.Equals("count", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(agg.Field) || agg.Field == "*"))
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

        if (options.GroupBy?.Count > 0)
        {
            foreach (var g in options.GroupBy)
            {
                selectParts.Add(QuoteColumn(mapping.GetColumnName(g), mapping));
            }
            return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
        }

        TraverseSelectTree(selectTree, mapping, mapping.TableAlias, string.Empty, selectParts);

        if (selectParts.Count == 0)
        {
            // Fallback if AST is totally empty
            foreach (var p in mapping.GetProperties())
            {
                selectParts.Add(QuoteColumn(mapping.GetColumnName(p), mapping));
            }
        }

        return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
    }

    private void TraverseSelectTree(SelectionNode node, IEntityMapping currentMapping, string? currentAlias, string prefix, List<string> selectParts)
    {
        bool hasSpecificFields = false;

        foreach (var child in node.EnumerateChildren())
        {
            var rel = currentMapping.GetRelationship(child.Key);
            if (rel != null)
            {
                // It's a navigation property
                var targetMapping = _mappingRegistry.GetMapping(rel.TargetType);
                var nextPrefix = prefix + rel.NavigationPropertyName + "_";
                var nextAlias = rel.NavigationPropertyName; // Dapper extensions use this

                TraverseSelectTree(child.Value, targetMapping, nextAlias, nextPrefix, selectParts);
            }
            else
            {
                // It's a regular property
                hasSpecificFields = true;
                var col = currentMapping.GetColumnName(child.Key);
                var outputName = child.Value.Alias ?? (prefix + col);
                
                var quotedAlias = string.IsNullOrEmpty(currentAlias) ? "" : _dialect.QuoteIdentifier(currentAlias) + ".";
                var quotedCol = _dialect.QuoteIdentifier(col);
                var quotedOutput = _dialect.QuoteIdentifier(outputName);
                
                selectParts.Add($"{quotedAlias}{quotedCol} AS {quotedOutput}");
            }
        }

        if (node.IncludeAllScalars || (!hasSpecificFields && !node.HasChildren))
        {
            foreach (var prop in currentMapping.GetProperties())
            {
                var col = currentMapping.GetColumnName(prop);
                var outputName = prefix + col;

                var quotedAlias = string.IsNullOrEmpty(currentAlias) ? "" : _dialect.QuoteIdentifier(currentAlias) + ".";
                var quotedCol = _dialect.QuoteIdentifier(col);
                var quotedOutput = _dialect.QuoteIdentifier(outputName);
                
                selectParts.Add($"{quotedAlias}{quotedCol} AS {quotedOutput}");
            }
        }
    }

    private string BuildJoinClause(QueryOptions options, IEntityMapping mapping, Dictionary<string, object?> parameters, SelectionNode selectTree)
    {
        var joins = new List<string>();
        var joinedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Infer joins from deep projection tree
        TraverseJoinTree(selectTree, mapping, mapping.TableAlias, joins, joinedPaths, parameters);

        // 2. Explicit Includes and Filtered Includes
        if (options.FilteredIncludes != null)
        {
            foreach (var filteredInclude in options.FilteredIncludes)
            {
                if (!joinedPaths.Add(filteredInclude.Path)) continue;

                var rel = mapping.GetRelationship(filteredInclude.Path);
                if (rel == null) continue;

                var node = new Ast.IncludeNode 
                { 
                    NavigationProperty = rel.NavigationPropertyName, 
                    Filter = filteredInclude.Filter 
                };
                
                var sql = _includeTranslator.Translate(node, mapping, filterGroup => 
                {
                    var targetMapping = _mappingRegistry.GetMapping(rel.TargetType!);
                    return BuildFilterGroupExpression(filterGroup, targetMapping, parameters);
                }, _mappingRegistry);

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
                var sql = _includeTranslator.Translate(node, mapping, _ => string.Empty, _mappingRegistry);
                if (!string.IsNullOrEmpty(sql)) joins.Add(sql);
            }
        }

        return string.Join(" ", joins);
    }

    private void TraverseJoinTree(SelectionNode node, IEntityMapping currentMapping, string? parentAlias, List<string> joins, HashSet<string> joinedPaths, Dictionary<string, object?> parameters)
    {
        foreach (var child in node.EnumerateChildren())
        {
            var rel = currentMapping.GetRelationship(child.Key);
            if (rel != null)
            {
                var childAlias = rel.NavigationPropertyName;
                
                if (joinedPaths.Add(childAlias))
                {
                    var targetMapping = _mappingRegistry.GetMapping(rel.TargetType);
                    
                    var parentRef = string.IsNullOrEmpty(parentAlias) ? currentMapping.TableName : parentAlias;
                    
                    string joinCondition = rel.RelationshipType switch
                    {
                        RelationshipType.OneToMany => $"{_dialect.QuoteIdentifier(childAlias)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(parentRef)}.{_dialect.QuoteIdentifier(currentMapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
                        RelationshipType.ManyToOne => $"{_dialect.QuoteIdentifier(parentRef)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(childAlias)}.{_dialect.QuoteIdentifier(targetMapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
                        _ => "1=0"
                    };

                    var sql = $"LEFT JOIN {_dialect.QuoteIdentifier(targetMapping.TableName)} AS {_dialect.QuoteIdentifier(childAlias)} ON {joinCondition}";
                    
                    if (child.Value.Filter != null)
                    {
                        var filterSql = BuildFilterGroupExpression(child.Value.Filter, targetMapping, parameters);
                        if (!string.IsNullOrEmpty(filterSql))
                            sql += $" AND ({filterSql})";
                    }

                    joins.Add(sql);
                    TraverseJoinTree(child.Value, targetMapping, childAlias, joins, joinedPaths, parameters);
                }
                else
                {
                    var targetMapping = _mappingRegistry.GetMapping(rel.TargetType);
                    TraverseJoinTree(child.Value, targetMapping, childAlias, joins, joinedPaths, parameters);
                }
            }
        }
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
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? _mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, _mappingRegistry);
        }
        
        if (op == FilterOperators.All && condition.ScopedFilter != null)
        {
            var node = new AllExpressionNode { NavigationProperty = condition.Field, ScopedFilter = condition.ScopedFilter };
            return _existsTranslator.TranslateAll(node, mapping, group => 
            {
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? _mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, _mappingRegistry);
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
            
            return _countTranslator.Translate(node, mapping, group => 
            {
                var rel = mapping.GetRelationship(condition.Field);
                var targetMapping = rel?.TargetType != null ? _mappingRegistry.GetMapping(rel.TargetType) : mapping;
                return BuildFilterGroupExpression(group, targetMapping, parameters);
            }, parameters, NextParam, _mappingRegistry);
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
                if (converter.CanConvertFrom(typeof(string)))
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

    private (string selectClause, string joinClause, List<string> flatJoins) BuildFlatSelectClause(
        QueryOptions options, IEntityMapping mapping, string distinctClause, SelectionNode selectTree)
    {
        var allowRootScalars = options.ProjectionMode == ProjectionMode.FlatMixed;
        var (navPath, fields) = DecomposeFlatSelection(selectTree, mapping, allowRootScalars);

        var distinctPrefix = !string.IsNullOrEmpty(distinctClause) ? $"{distinctClause} " : string.Empty;
        var selectParts = new List<string>();
        var flatJoins = new List<string>();
        var joins = new List<string>();

        if (navPath.Count == 0)
        {
            var dialectTable = _dialect.QuoteIdentifier(mapping.TableName);

            foreach (var f in fields)
            {
                var col = f.Mapping.GetColumnName(f.PropName);
                selectParts.Add($"{dialectTable}.{_dialect.QuoteIdentifier(col)} AS {_dialect.QuoteIdentifier(f.OutputName)}");
            }

            if (selectParts.Count == 0)
            {
                foreach (var propName in mapping.GetProperties())
                {
                    var col = mapping.GetColumnName(propName);
                    selectParts.Add($"{dialectTable}.{_dialect.QuoteIdentifier(col)}");
                }
            }

            return ($"SELECT {distinctPrefix}{string.Join(", ", selectParts)}", string.Empty, flatJoins);
        }

        var currentAlias = mapping.TableAlias ?? mapping.TableName;
        var currentMapping = mapping;
        var rootTable = _dialect.QuoteIdentifier(mapping.TableName);

        // Project root scalars (level -1) for FlatMixed mode
        if (allowRootScalars)
        {
            foreach (var f in fields.Where(f => f.Level == -1))
            {
                var col = f.Mapping.GetColumnName(f.PropName);
                selectParts.Add($"{rootTable}.{_dialect.QuoteIdentifier(col)} AS {_dialect.QuoteIdentifier(f.OutputName)}");
            }
        }

        for (var i = 0; i < navPath.Count; i++)
        {
            var navName = navPath[i];
            var rel = currentMapping.GetRelationship(navName);
            if (rel == null) continue;

            var targetMapping = _mappingRegistry.GetMapping(rel.TargetType);
            var navAlias = rel.NavigationPropertyName;

            var joinCondition = rel.RelationshipType switch
            {
                RelationshipType.OneToMany => $"{_dialect.QuoteIdentifier(navAlias)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(currentAlias)}.{_dialect.QuoteIdentifier(currentMapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
                RelationshipType.ManyToOne => $"{_dialect.QuoteIdentifier(currentAlias)}.{_dialect.QuoteIdentifier(rel.ForeignKey)} = {_dialect.QuoteIdentifier(navAlias)}.{_dialect.QuoteIdentifier(targetMapping.GetColumnName(rel.PrincipalKey ?? "Id"))}",
                _ => "1=0"
            };

            joins.Add($"LEFT JOIN {_dialect.QuoteIdentifier(targetMapping.TableName)} AS {_dialect.QuoteIdentifier(navAlias)} ON {joinCondition}");
            flatJoins.Add(navAlias);

            if (allowRootScalars)
            {
                foreach (var f in fields.Where(f => f.Level == i))
                {
                    var col = f.Mapping.GetColumnName(f.PropName);
                    selectParts.Add($"{_dialect.QuoteIdentifier(navAlias)}.{_dialect.QuoteIdentifier(col)} AS {_dialect.QuoteIdentifier(f.OutputName)}");
                }
            }

            currentMapping = targetMapping;
            currentAlias = navAlias;
        }

        foreach (var f in fields.Where(f => f.Level == navPath.Count))
        {
            var col = f.Mapping.GetColumnName(f.PropName);
            selectParts.Add($"{_dialect.QuoteIdentifier(currentAlias)}.{_dialect.QuoteIdentifier(col)} AS {_dialect.QuoteIdentifier(f.OutputName)}");
        }

        if (selectParts.Count == 0)
        {
            foreach (var propName in currentMapping.GetProperties())
            {
                var col = currentMapping.GetColumnName(propName);
                selectParts.Add($"{_dialect.QuoteIdentifier(currentAlias)}.{_dialect.QuoteIdentifier(col)}");
            }
        }

        var joinClause = string.Join(" ", joins);
        return ($"SELECT {distinctPrefix}{string.Join(", ", selectParts)}", joinClause, flatJoins);
    }

    private record FlatField(int Level, string OutputName, string PropName, IEntityMapping Mapping);

    private (List<string> navPath, List<FlatField> fields) DecomposeFlatSelection(
        SelectionNode node, IEntityMapping mapping, bool allowRootScalars, int level = 0)
    {
        var navPath = new List<string>();
        var fields = new List<FlatField>();

        var navChildren = new List<(string name, SelectionNode child, RelationshipMapping rel)>();
        var scalarChildren = new List<(string name, SelectionNode child)>();

        foreach (var child in node.EnumerateChildren())
        {
            var rel = mapping.GetRelationship(child.Key);
            if (rel != null)
            {
                navChildren.Add((child.Key, child.Value, rel));
            }
            else
            {
                scalarChildren.Add((child.Key, child.Value));
            }
        }

        if (!allowRootScalars && navChildren.Count > 1)
            throw new InvalidOperationException(
                "Flat mode does not support branching multiple navigation paths. Select a single linear path.");

        if (navChildren.Count == 0)
        {
            foreach (var (propName, childNode) in scalarChildren)
            {
                var outputName = childNode.Alias ?? propName;
                fields.Add(new FlatField(level, outputName, propName, mapping));
            }

            if (node.IncludeAllScalars || node.HasChildren == false)
            {
                foreach (var propName in mapping.GetProperties())
                {
                    var prop = mapping.Type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null && TypeClassification.IsScalarType(prop.PropertyType))
                    {
                        var outputName = propName;
                        fields.Add(new FlatField(level, outputName, propName, mapping));
                    }
                }
            }
        }

        foreach (var (navName, navNode, rel) in navChildren)
        {
            var targetMapping = _mappingRegistry.GetMapping(rel.TargetType);
            navPath.Add(rel.NavigationPropertyName);

            var (subPath, subFields) = DecomposeFlatSelection(navNode, targetMapping, allowRootScalars, level + 1);
            navPath.AddRange(subPath);
            fields.AddRange(subFields);

            if (allowRootScalars)
            {
                foreach (var (propName, childNode) in scalarChildren)
                {
                    var outputName = childNode.Alias ?? propName;
                    fields.Add(new FlatField(-1, outputName, propName, mapping));
                }
            }
        }

        return (navPath, fields);
    }
}
