using System.Text.RegularExpressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Converters;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Dapper.Sql.Translators;

/// <summary>
/// SQL translator implementation that generates parameterized queries from QueryOptions.
/// Acts as an orchestrator: it resolves the entity mapping and selection tree once per
/// translation, then delegates SELECT, JOIN, and WHERE generation to dedicated builders
/// and assembles their output into the final SQL string. GROUP BY, HAVING, ORDER BY, and
/// paging remain here directly — each is a few lines with no recursive structure, so a
/// dedicated class for any of them would be ceremony without payoff.
/// All parameter naming and SQL generation is delegated to the ISqlDialect abstraction.
/// </summary>
internal sealed class SqlTranslator : ISqlTranslator
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
        var whereClause = _whereBuilder.BuildWhereClause(options.Filter, mapping, parameters, options.CaseInsensitive);
        var groupByClause = BuildGroupByClause(options.GroupBy, mapping);
        var havingClause = SqlHavingBuilder.Build(_dialect, options.Having, mapping, parameters);

        var sortForOrderBy = options.GroupBy?.Count > 0
            ? GroupedSortValidator.ValidateOrThrow(options.Sort, options.GroupBy, options.Aggregates)
            : options.Sort;

        string orderByClause;
        string pagingClause;

        if (options.IsKeysetMode)
        {
            if (sortForOrderBy.Count == 0)
                throw new InvalidOperationException("Keyset pagination requires at least one sort field.");

            if (options.Cursor != null)
            {
                var keysetResult = SqlKeysetBuilder.BuildSeekClause(sortForOrderBy, options.Cursor, mapping, _dialect, parameters);

                if (!string.IsNullOrEmpty(keysetResult.WhereClause))
                {
                    whereClause = string.IsNullOrEmpty(whereClause)
                        ? $"WHERE {keysetResult.WhereClause}"
                        : $"{whereClause} AND {keysetResult.WhereClause}";
                }

                orderByClause = keysetResult.OrderByClause;
            }
            else
            {
                orderByClause = BuildOrderByClause(sortForOrderBy, mapping);
            }

            pagingClause = options.Paging.Disabled
                ? string.Empty
                : BuildKeysetLimitClause(options.Paging, parameters);
        }
        else
        {
            if (!options.Paging.Disabled
                && sortForOrderBy is { Count: 0 })
            {
                if (_dialect.RequiresOrderByForPaging)
                {
                    throw new InvalidOperationException(
                        $"Paging requires an ORDER BY clause when using the {_dialect.GetType().Name} dialect. "
                        + "Add at least one Sort field to QueryOptions.Sort, or set Paging.Disabled = true.");
                }

                sortForOrderBy = [new SortNode { Field = ResolveDefaultSortProperty(mapping) }];
            }

            orderByClause = BuildOrderByClause(sortForOrderBy, mapping);
            pagingClause = BuildPagingClause(options.Paging, parameters);
        }

        var clauses = new List<string> { selectClause, fromClause, joinClause, whereClause, groupByClause, havingClause, orderByClause, pagingClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = Regex.Replace(sql, @"\s+", " ");

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
        var whereClause = _whereBuilder.BuildWhereClause(options.Filter, mapping, parameters, options.CaseInsensitive);

        var clauses = new List<string> { selectClause, fromClause, joinClause, whereClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = Regex.Replace(sql, @"\s+", " ");

        return new SqlCommand
        {
            Sql = sql,
            Parameters = parameters.RawParameters
        };
    }

    /// <summary>Translates filters into a source-record count SQL command.</summary>
    internal SqlCommand TranslateSourceCount(QueryOptions options)
    {
        var (mapping, selectTree) = PrepareTranslation(options);
        var parameters = new SqlParameterContext(_dialect);

        var fromClause = BuildFromClause(mapping);
        var joinClause = _joinBuilder.BuildJoinClause(options, mapping, parameters, selectTree);
        var whereClause = _whereBuilder.BuildWhereClause(options.Filter, mapping, parameters, options.CaseInsensitive);

        var clauses = new List<string> { $"SELECT {_dialect.GetCountExpression}", fromClause, joinClause, whereClause };
        var sql = string.Join(" ", clauses.Where(c => !string.IsNullOrEmpty(c)));
        sql = Regex.Replace(sql, @"\s+", " ");

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

        if (options.Includes?.Count > 0 || options.Expand?.Count > 0 || selectTree.HasChildren)
        {
            mapping.TableAlias = mapping.TableName;
        }

        return (mapping, selectTree);
    }

    private string BuildFromClause(IEntityMapping mapping)
    {
        var quotedTable = SqlSyntaxBuilder.QuoteTable(_dialect, mapping);
        return string.IsNullOrEmpty(mapping.TableAlias)
            ? $"FROM {quotedTable}"
            : $"FROM {quotedTable} AS {_dialect.QuoteIdentifier(mapping.TableAlias)}";
    }

    private string BuildGroupByClause(IReadOnlyList<string>? groupBys, IEntityMapping mapping)
    {
        if (groupBys == null || groupBys.Count == 0) return string.Empty;
        var columns = groupBys.Select(g => SqlSyntaxBuilder.QuoteColumn(_dialect, mapping.GetColumnName(g), mapping));
        return $"GROUP BY {string.Join(", ", columns)}";
    }

    private string BuildOrderByClause(IReadOnlyList<SortNode>? sorts, IEntityMapping mapping)
    {
        if (sorts == null || sorts.Count == 0) return string.Empty;
        var columns = sorts.Select(s =>
        {
            var column = ResolveOrderByExpression(s, mapping);
            return s.Descending ? $"{column} DESC" : column;
        });
        return $"ORDER BY {string.Join(", ", columns)}";
    }

    private string ResolveOrderByExpression(SortNode sort, IEntityMapping mapping)
    {
        if (mapping.GetPropertyName(sort.Field) is not null)
        {
            return SqlSyntaxBuilder.QuoteColumn(_dialect, mapping.GetColumnName(sort.Field), mapping);
        }

        return _dialect.QuoteIdentifier(sort.Field);
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

    private string BuildKeysetLimitClause(PagingOptions paging, SqlParameterContext parameters)
    {
        var limitParam = _dialect.CreateParameterName("PageSize");
        parameters.AddNamed(limitParam, paging.PageSize);

        return _dialect.GetLimitExpression(limitParam);
    }

    private static string ResolveDefaultSortProperty(IEntityMapping mapping)
    {
        var properties = mapping.GetProperties().ToList();

        var pkProperty = properties.FirstOrDefault(p =>
            p.Equals("Id", StringComparison.OrdinalIgnoreCase)
            || p.Equals("Key", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        return pkProperty ?? properties.FirstOrDefault() ?? "Id";
    }
}
