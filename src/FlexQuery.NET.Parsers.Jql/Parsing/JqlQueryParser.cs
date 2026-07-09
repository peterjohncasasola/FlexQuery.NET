using FlexQuery.NET.Filters;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Parses JQL (Jira Query Language) filter expressions into the unified FlexQuery
/// <see cref="FilterGroup"/> and <see cref="QueryOptions"/> models.
/// <para>
/// JQL syntax examples:
/// <c>status = 'Open'</c>,
/// <c>priority IN (High, Critical)</c>,
/// <c>assignee IS NOT NULL</c>,
/// <c>labels CONTAINS 'bug'</c>.
/// </para>
/// </summary>
internal sealed class JqlQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Jql;

    /// <summary>
    /// Parses a raw JQL filter string into a <see cref="FilterGroup"/> AST.
    /// Internally tokenizes the input, builds a JQL AST, and converts it
    /// into the unified filter model.
    /// </summary>
    /// <param name="filter">The JQL filter expression string.</param>
    /// <returns>A <see cref="FilterGroup"/> representing the parsed filter logic.</returns>
    /// <exception cref="JqlParseException">Thrown when the input contains invalid JQL syntax.</exception>
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
        return options;
    }
}
