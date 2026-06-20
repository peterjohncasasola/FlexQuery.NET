using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Helpers;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Helpers;

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
/// Acts as an orchestrator: it resolves the entity mapping and selection tree once per
/// translation, then delegates SELECT, JOIN, and WHERE generation to dedicated builders
/// and assembles their output into the final SQL string. GROUP BY, HAVING, ORDER BY, and
/// paging remain here directly — each is a few lines with no recursive structure, so a
/// dedicated class for any of them would be ceremony without payoff.
/// All parameter naming and SQL generation is delegated to the ISqlDialect abstraction.
/// </summary>
public sealed class SqlTranslator : ISqlTranslator
{
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ISqlDialect _dialect;
    private readonly SqlSelectBuilder _selectBuilder;
    private readonly SqlJoinBuilder _joinBuilder;
    private readonly SqlWhereBuilder _whereBuilder;

    /// <summary>Creates a new SQL translator.</summary>
    public SqlTranslator(IMappingRegistry mappingRegistry, ISqlDialect dialect)
    {
        _mappingRegistry = mappingRegistry;
        _dialect = dialect;

        var includeTranslator = new SqlIncludeTranslator(dialect);
        var existsTranslator = new SqlExistsTranslator(dialect);
        var countTranslator = new SqlCountTranslator(dialect);

        _selectBuilder = new SqlSelectBuilder(mappingRegistry, dialect);
        _whereBuilder = new SqlWhereBuilder(mappingRegistry, dialect, existsTranslator, countTranslator);
        _joinBuilder = new SqlJoinBuilder(mappingRegistry, dialect, includeTranslator, _whereBuilder);
    }

    /// <summary>Translates the query options into a complete SQL command with parameters.</summary>
    public SqlCommand Translate(QueryOptions options)
    {
        var (mapping, selectTree) = PrepareTranslation(options);
        var parameters = new SqlParameterContext(_dialect);

        var distinctClause = options.Distinct == true ? "DISTINCT" : string.Empty;

        string selectClause;
        string joinClause;
        List<string>? flatJoins = null;

        if (options.ProjectionMode is ProjectionMode.Flat or ProjectionMode.FlatMixed)
        {
            (selectClause, joinClause, flatJoins) = _selectBuilder.BuildFlatSelectClause(options, mapping, distinctClause, selectTree);
        }
        else
        {
            selectClause = _selectBuilder.BuildSelectClause(options, mapping, distinctClause, selectTree);
            joinClause = _joinBuilder.BuildJoinClause(options, mapping, parameters, selectTree);
        }

        var fromClause = BuildFromClause(mapping);
        var whereClause = _whereBuilder.BuildWhereClause(options.Filter, mapping, parameters);
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
            Parameters = parameters.RawParameters,
            FlatJoins = flatJoins
        };
    }

    /// <summary>Translates aggregates list into parameterized SQL.</summary>
    public SqlCommand TranslateAggregates(QueryOptions options)
    {
        var (mapping, selectTree) = PrepareTranslation(options);
        var parameters = new SqlParameterContext(_dialect);

        var selectParts = _selectBuilder.BuildAggregateSelectParts(options, mapping);
        var selectClause = $"SELECT {string.Join(", ", selectParts)}";

        var fromClause = BuildFromClause(mapping);
        var joinClause = _joinBuilder.BuildJoinClause(options, mapping, parameters, selectTree);
        var whereClause = _whereBuilder.BuildWhereClause(options.Filter, mapping, parameters);

        var clauses = new List<string> { selectClause, fromClause, joinClause, whereClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ");

        return new SqlCommand
        {
            Sql = sql,
            Parameters = parameters.RawParameters
        };
    }

    /// <summary>
    /// Resolves the entity mapping and select tree for a query, aligning the table alias
    /// when joins or nested projections are present.
    /// </summary>
    private (IEntityMapping mapping, SelectionNode selectTree) PrepareTranslation(QueryOptions options)
    {
        var entityType = options.Items.TryGetValue(ContextKeys.EntityType, out var type) ? (Type)type : typeof(object);
        var mapping = _mappingRegistry.GetMapping(entityType);
        var selectTree = SelectTreeBuilder.Build(options);

        if (options.Includes?.Count > 0 || options.FilteredIncludes?.Count > 0 || selectTree.HasChildren)
        {
            mapping.TableAlias = mapping.TableName;
        }

        return (mapping, selectTree);
    }

    private string BuildFromClause(IEntityMapping mapping)
    {
        return string.IsNullOrEmpty(mapping.TableAlias)
            ? $"FROM {_dialect.QuoteIdentifier(mapping.TableName)}"
            : $"FROM {_dialect.QuoteIdentifier(mapping.TableName)} AS {_dialect.QuoteIdentifier(mapping.TableAlias)}";
    }

    private string BuildGroupByClause(IReadOnlyList<string>? groupBys, IEntityMapping mapping)
    {
        if (groupBys == null || groupBys.Count == 0) return string.Empty;
        var columns = groupBys.Select(g => SqlDialectHelper.QuoteColumn(_dialect, mapping.GetColumnName(g), mapping));
        return $"GROUP BY {string.Join(", ", columns)}";
    }

    private string BuildHavingClause(HavingCondition? having, IEntityMapping mapping, SqlParameterContext parameters)
    {
        if (having == null) return string.Empty;
        var column = SqlDialectHelper.QuoteColumn(_dialect, mapping.GetColumnName(having.Field ?? "*"), mapping);

        var valStr = having.Value?.ToString()?.Trim('"');
        var paramName = parameters.Add(SqlValueConverter.Convert(having.Field ?? string.Empty, valStr, mapping));

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
            var column = SqlDialectHelper.QuoteColumn(_dialect, mapping.GetColumnName(s.Field), mapping);
            return s.Descending ? $"{column} DESC" : column;
        });
        return $"ORDER BY {string.Join(", ", columns)}";
    }

    private string BuildPagingClause(PagingOptions paging, SqlParameterContext parameters)
    {
        if (paging.Disabled) return string.Empty;

        var offset = (paging.Page - 1) * paging.PageSize;
        var offsetParam = _dialect.CreateParameterName("Offset");
        var limitParam = _dialect.CreateParameterName("PageSize");
        parameters.AddNamed(offsetParam, offset);
        parameters.AddNamed(limitParam, paging.PageSize);

        return _dialect.GetPagingClause(offsetParam, limitParam);
    }
}