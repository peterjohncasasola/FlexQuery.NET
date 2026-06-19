using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Shared helper for parsing generic indexed query parameters.
/// </summary>
internal static class IndexedFilterParser
{
    public static bool HasIndexFilters(IDictionary<string, string>? parameters)
    {
        return parameters is not null
            && parameters.Keys.Any(k => k.StartsWith("filter[0]", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasIndexedSort(IDictionary<string, string>? parameters)
    {
        return parameters is not null
            && parameters.Keys.Any(k => k.StartsWith("sort[0]", StringComparison.OrdinalIgnoreCase));
    }

    public static QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);
        var filterGroup = FilterParser.Parse(parameters.RawParameters!);
        if (filterGroup != null)
        {
            options.Filter = filterGroup;
        }
        options.Sort.AddRange(SortParser.Parse(parameters.RawParameters!));
        return options;
    }
}
