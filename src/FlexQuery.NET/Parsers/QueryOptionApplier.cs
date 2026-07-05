using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

internal static class QueryOptionApplier
{
    public static void ApplyProjectionAndExecutionOptions(
        FlexQueryParameters parameters,
        QueryOptions options,
        bool parseSortBeforeGrouping)
    {
        ApplySelect(options, parameters.Select);

        if (parseSortBeforeGrouping)
        {
            ApplySort(options, parameters.Sort);
        }

        ApplyGrouping(options, parameters.GroupBy);
        ApplyHaving(options, parameters.Having);
        ApplyIncludes(options, parameters.Include);

        if (!parseSortBeforeGrouping)
        {
            ApplySort(options, parameters.Sort);
        }
    }

    public static void ApplyRawProjectionAndExecutionOptions(
        IDictionary<string, string> parameters,
        QueryOptions options)
    {
        if (parameters.TryGetValue(QueryOptionKeys.Select, out var select))
        {
            SelectParser.Parse(options, select);
        }

        if (parameters.TryGetValue(QueryOptionKeys.Group, out var groupBy))
        {
            options.GroupBy = ParserUtilities.SplitCsv(groupBy);
        }

        if (parameters.TryGetValue(QueryOptionKeys.Having, out var having))
        {
            options.Having = HavingParser.Parse(having);
        }

        if (parameters.TryGetValue(QueryOptionKeys.Include, out var include))
        {
            options.Includes = ParserUtilities.SplitCsv(include.Split('(')[0]);
            options.Expand = FilteredIncludeParser.Parse(include);
        }

        options.Sort.AddRange(SortParser.Parse(parameters));
    }

    private static void ApplySelect(QueryOptions options, string? select)
    {
        if (!string.IsNullOrWhiteSpace(select))
        {
            SelectParser.Parse(options, select);
        }
    }

    private static void ApplyGrouping(QueryOptions options, string? groupBy)
    {
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            options.GroupBy = ParserUtilities.SplitCsv(groupBy);
        }
    }

    private static void ApplyHaving(QueryOptions options, string? having)
    {
        if (!string.IsNullOrWhiteSpace(having))
        {
            options.Having = HavingParser.Parse(having);
        }
    }

    private static void ApplyIncludes(QueryOptions options, string? include)
    {
        if (!string.IsNullOrWhiteSpace(include))
        {
            options.Includes = ParserUtilities.SplitCsv(include.Split('(')[0]);
            options.Expand = FilteredIncludeParser.Parse(include);
        }
    }

    private static void ApplySort(QueryOptions options, string? sort)
    {
        if (!string.IsNullOrWhiteSpace(sort))
        {
            options.Sort.AddRange(SortParser.Parse(sort));
        }
    }
}
