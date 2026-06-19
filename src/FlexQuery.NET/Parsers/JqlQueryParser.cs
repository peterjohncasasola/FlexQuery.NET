using FlexQuery.NET.Models;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Parsers.Jql.Ast;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Legacy implementation of <see cref="IQueryParser"/> that handles JQL-lite syntax.
/// </summary>
public sealed class JqlQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Jql;

    /// <inheritdoc />
    public bool CanParse(FlexQueryParameters parameters)
    {
        // JQL is detected by the presence of the 'Query' property (query=...)
        return !string.IsNullOrWhiteSpace(parameters.Query);
    }

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);
        if (!string.IsNullOrWhiteSpace(parameters.Query))
        {
            var ast = JqlAstParser.Parse(parameters.Query);
            options.Filter = JqlFilterConverter.ToFilterGroup(ast);
            options.Ast = ast;
            options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
        }
        return options;
    }
}
