using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Filters;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Parses JQL (Jira Query Language) expressions into the unified FlexQuery
/// <see cref="QueryOptions"/> model.
/// <para>
/// JQL is a SQL-inspired query language supporting filter, sort, groupBy,
/// aggregates, and having — all with a consistent grammar.
/// </para>
/// </summary>
internal sealed class JqlQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Jql;

    /// <summary>
    /// Parses a raw JQL filter string into a <see cref="FilterGroup"/> AST.
    /// </summary>
    public FilterGroup Parse(string filter)
    {
        var ast = JqlAstParser.Parse(filter);
        return JqlFilterConverter.ToFilterGroup(ast);
    }

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            try
            {
                var ast = JqlAstParser.Parse(parameters.Filter);
                options.Filter = JqlFilterConverter.ToFilterGroup(ast);
                options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("filter", QuerySyntax.Jql, parameters.Filter, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            try
            {
                options.Sort = JqlSortParser.Parse(parameters.Sort);
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("sort", QuerySyntax.Jql, parameters.Sort, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
        {
            try
            {
                options.GroupBy = JqlGroupByParser.Parse(parameters.GroupBy);
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("groupBy", QuerySyntax.Jql, parameters.GroupBy, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Aggregates))
        {
            try
            {
                options.Aggregates.Clear();
                options.Aggregates.AddRange(JqlAggregateParser.Parse(parameters.Aggregates));
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("aggregates", QuerySyntax.Jql, parameters.Aggregates, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Having))
        {
            try
            {
                options.Having = JqlHavingParser.Parse(parameters.Having);
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("having", QuerySyntax.Jql, parameters.Having, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            try
            {
                options.Expand = JqlIncludeParser.Parse(parameters.Include);
            }
            catch (JqlParseException ex)
            {
                throw new QueryParseException("include", QuerySyntax.Jql, parameters.Include, ex);
            }
        }

        return options;
    }
}
