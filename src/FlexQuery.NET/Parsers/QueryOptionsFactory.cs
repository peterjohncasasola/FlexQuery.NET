using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers.Dsl;
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

        var groupBy = GroupByParser.Parse(parameters.GroupBy);
        options.GroupBy = groupBy;

        options.IsKeysetMode = isKeyset;
        options.OffsetExplicitlyRequested = parameters.Page != null;
        if (parameters.Cursor != null)
            options.Cursor = KeysetCursorSerializer.Deserialize(parameters.Cursor);

        return options;
    }
}