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
            var ast = JqlAstParser.Parse(parameters.Filter);
            options.Filter = JqlFilterConverter.ToFilterGroup(ast);
            options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
            options.Sort = JqlSortParser.Parse(parameters.Sort);

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
            options.GroupBy = JqlGroupByParser.Parse(parameters.GroupBy);

        if (!string.IsNullOrWhiteSpace(parameters.Aggregates))
        {
            options.Aggregates.Clear();
            options.Aggregates.AddRange(JqlAggregateParser.Parse(parameters.Aggregates));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Having))
            options.Having = JqlHavingParser.Parse(parameters.Having);

        return options;
    }
}
