using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Factory for creating base <see cref="QueryOptions"/> instances with common properties.
/// Centralizes the creation of pagination, mode, select, include, and other shared options.
/// </summary>
internal static class QueryOptionsFactory
{
    /// <summary>
    /// Creates a base QueryOptions populated with paging, mode, select, include, group, and having options.
    /// </summary>
    public static QueryOptions Create(FlexQueryParameters parameters)
    {
        var options = new QueryOptions
        {
            Paging = new PagingOptions
            {
                Page = parameters.Page ?? 1,
                PageSize = parameters.PageSize ?? 20
            },
            ProjectionMode = ParserUtilities.ParseProjectionMode(parameters.Mode),
            IncludeCount = parameters.IncludeCount ?? true,
            Distinct = parameters.Distinct ?? false
        };

        if (!string.IsNullOrWhiteSpace(parameters.Select))
            SelectParser.Parse(options, parameters.Select);

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
            options.GroupBy = ParserUtilities.SplitCsv(parameters.GroupBy);

        if (!string.IsNullOrWhiteSpace(parameters.Having))
            options.Having = HavingParser.Parse(parameters.Having);

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            options.Includes = ParserUtilities.SplitCsv(parameters.Include.Split('(')[0]);
            options.Expand = FilteredIncludeParser.Parse(parameters.Include);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
            options.Sort.AddRange(SortParser.Parse(parameters.Sort));

        return options;
    }
}