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

        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
        {
            var groups = ParserUtilities.SplitCsv(parameters.GroupBy);
            foreach (var g in groups)
            {
                if (!ParserUtilities.IsValidPropertyPath(g.AsSpan()))
                    throw new DslParseException(
                        $"Invalid property path '{g}' in 'group' parameter. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Category' or 'Customer.Region').");
            }
            options.GroupBy = groups;
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            var includePaths = ParserUtilities.SplitCsv(parameters.Include.Split('(')[0]);
            foreach (var inc in includePaths)
            {
                if (!ParserUtilities.IsValidPropertyPath(inc.AsSpan()))
                    throw new DslParseException(
                        $"Invalid navigation property path '{inc}' in include expression. " +
                        "Property paths must be dot-separated identifiers (e.g. 'Orders' or 'Orders.Items').");
            }
            options.Includes = includePaths;
        }

        options.IsKeysetMode = isKeyset;
        options.OffsetExplicitlyRequested = parameters.Page != null;
        if (parameters.Cursor != null)
            options.Cursor = KeysetCursorSerializer.Deserialize(parameters.Cursor);

        return options;
    }
}