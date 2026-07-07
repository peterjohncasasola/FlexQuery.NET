using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Serialization;

namespace FlexQuery.NET.Parsers;

internal static class QueryOptionsFactory
{
    public static QueryOptions Create(FlexQueryParameters parameters)
    {
        var isKeyset = parameters.UseKeysetPagination || parameters.Cursor != null;

        var options = new QueryOptions
        {
            Paging = new PagingOptions
            {
                Page = parameters.Page ?? 1,
                PageSize = parameters.PageSize ?? 20
            },
            ProjectionMode = ParserUtilities.ParseProjectionMode(parameters.Mode),
            IncludeCount = parameters.IncludeCount ?? (isKeyset ? null : true),
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

        options.IsKeysetMode = isKeyset;
        options.OffsetExplicitlyRequested = parameters.Page != null;
        if (parameters.Cursor != null)
            options.Cursor = KeysetCursorSerializer.Deserialize(parameters.Cursor);

        return options;
    }
}