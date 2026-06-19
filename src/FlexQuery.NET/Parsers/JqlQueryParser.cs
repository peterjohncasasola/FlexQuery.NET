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
        // Use the internal static helper from QueryOptionsParser for consistency.
        // If RawParameters exist, we use the dictionary-based JQL parser.
        if (parameters.RawParameters != null && parameters.RawParameters.Count > 0)
        {
            return QueryParameterParser.Parse(parameters.RawParameters);
        }

        // Fallback for manual DTO instantiation without RawParameters.
        var options = new QueryOptions
        {
            Paging =
            {
                // Populate standard options from properties
                Page = parameters.Page ?? 1,
                PageSize = parameters.PageSize ?? 20
            },
            IncludeCount = parameters.IncludeCount ?? true,
            Distinct = parameters.Distinct ?? false
        };

        if (!string.IsNullOrWhiteSpace(parameters.Select))
            SelectParser.Parse(options, parameters.Select);

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
            options.Sort.AddRange(SortParser.Parse(parameters.Sort));

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
            options.GroupBy = ParserUtilities.SplitCsv(parameters.GroupBy);

        if (!string.IsNullOrWhiteSpace(parameters.Having))
            options.Having = HavingParser.Parse(parameters.Having);

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            options.Includes = ParserUtilities.SplitCsv(parameters.Include.Split('(')[0]);
            options.FilteredIncludes = FilteredIncludeParser.Parse(parameters.Include);
        }

        // Parse JQL Query
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
