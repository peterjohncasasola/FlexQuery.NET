using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;

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
    public static QueryOptions CreateBase(IDictionary<string, string> source)
    {
        return new QueryOptions
        {
            Paging = new PagingOptions
            {
                Page = ParserUtilities.ParseInt(source, QueryOptionKeys.Page, 1),
                PageSize = ParserUtilities.ParseInt(source, QueryOptionKeys.PageSize, 20)
            },
            ProjectionMode = ParserUtilities.ParseProjectionMode(GetValueOrDefault(source, QueryOptionKeys.Mode)),
            GroupBy = ParserUtilities.SplitCsv(GetValueOrDefault(source, QueryOptionKeys.Group)),
            Having = HavingParser.Parse(GetValueOrDefault(source, QueryOptionKeys.Having)),
            IncludeCount = ParserUtilities.ParseBool(GetValueOrDefault(source, QueryOptionKeys.IncludeCount), true),
            Distinct = ParserUtilities.ParseBool(GetValueOrDefault(source, QueryOptionKeys.Distinct))
        };
    }

    public static QueryOptions CreateBase(FlexQueryParameters parameters)
    {
        return new QueryOptions
        {
            Paging = new PagingOptions
            {
                Page = parameters.Page ?? 1,
                PageSize = parameters.PageSize ?? 20
            },
            ProjectionMode = ParserUtilities.ParseProjectionMode(parameters.Mode),
            GroupBy = ParserUtilities.SplitCsv(parameters.GroupBy),
            Having = HavingParser.Parse(parameters.Having),
            IncludeCount = parameters.IncludeCount ?? true,
            Distinct = parameters.Distinct ?? false
        };
    }

    private static string? GetValueOrDefault(IDictionary<string, string> source, string key)
    {
        return source.TryGetValue(key, out var value) ? value : null;
    }
}