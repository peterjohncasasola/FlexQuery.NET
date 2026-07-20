using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Filters;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL (FlexQuery Language) expressions into the unified FlexQuery
/// <see cref="QueryOptions"/> model.
/// <para>
/// Fql is a SQL-inspired query language supporting filter, sort, groupBy,
/// aggregates, and having — all with a consistent grammar.
/// </para>
/// </summary>
internal sealed class FqlQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Fql;

    /// <summary>
    /// Parses a raw Fql filter string into a <see cref="FilterGroup"/> AST.
    /// </summary>
    public FilterGroup Parse(string filter)
    {
        var ast = FqlAstParser.Parse(filter);
        return FqlFilterConverter.ToFilterGroup(ast);
    }

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);

        if (!string.IsNullOrWhiteSpace(parameters.Select))
        {
            try
            {
                FqlSelectParser.Parse(options, parameters.Select);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Select,
                    QuerySyntax.Fql,
                    parameters.Select,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            try
            {
                var ast = FqlAstParser.Parse(parameters.Filter);
                options.Filter = FqlFilterConverter.ToFilterGroup(ast);
                options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    "filter",
                    QuerySyntax.Fql,
                    parameters.Filter,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            try
            {
                options.Sort = FqlSortParser.Parse(parameters.Sort);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Sort,
                    QuerySyntax.Fql,
                    parameters.Sort,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
        {
            try
            {
                options.GroupBy = GroupByParser.Parse(parameters.GroupBy);
            }
            catch (FlexQueryParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.GroupBy,
                    QuerySyntax.Fql,
                    parameters.GroupBy,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Aggregate))
        {
            try
            {
                options.Aggregates.Clear();
                options.Aggregates.AddRange(FqlAggregateParser.Parse(parameters.Aggregate));
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Aggregate,
                    QuerySyntax.Fql,
                    parameters.Aggregate,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Having))
        {
            try
            {
                options.Having = FqlHavingParser.Parse(parameters.Having);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Having,
                    QuerySyntax.Fql,
                    parameters.Having,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            try
            {
                options.Includes = FqlIncludeParser.Parse(parameters.Include);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Include,
                    QuerySyntax.Fql,
                    parameters.Include,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Expand))
        {
            try
            {
                var expandAst = FqlExpandParser.Parse(parameters.Expand);
                options.Expand = ExpandNormalizer.Normalize(expandAst);
            }
            catch (FqlParseException ex)
            {
                throw new QueryParseException(
                    QueryOptionKeys.Expand,
                    QuerySyntax.Fql,
                    parameters.Expand,
                    ex,
                    position: ex.Position,
                    expected: ex.Expected,
                    found: ex.Found);
            }
        }

        return options;
    }
}
