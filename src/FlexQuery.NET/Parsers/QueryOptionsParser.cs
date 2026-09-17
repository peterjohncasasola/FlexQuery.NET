using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Parsers;

internal static class QueryOptionsParser
{
    public static QueryOptions Parse(FlexQueryParameters parameters, QuerySyntax? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var effectiveSyntax = syntax ?? QuerySyntax.NativeDsl;
        var parser = QueryParserRegistry.Resolve(effectiveSyntax);

        var cacheKey = new ParsedQueryCacheKey(
            parameters.Filter, parameters.Sort, parameters.Select,
            parameters.Include, parameters.GroupBy, parameters.Having,
            parameters.Page, parameters.PageSize, parameters.IncludeCount,
            parameters.Distinct, parameters.Mode, parameters.Cursor, parameters.UseKeysetPagination,
            Version: effectiveSyntax.ToString(), Aggregates: parameters.Aggregate, Expand: parameters.Expand);

        if (ParserCache.TryGet(cacheKey, out var cached))
            return cached!;

        var parsedOptions = parser.Parse(parameters);
        ParserCache.Set(cacheKey, parsedOptions);
        return parsedOptions;
    }

    public static QueryOptions Parse(
        IReadOnlyDictionary<string, StringValues> queryString,
        QuerySyntax? syntax = null)
    {
        var grouped = queryString.GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value.ToString(), StringComparer.OrdinalIgnoreCase);

        string? TryGet(string key) => grouped.GetValueOrDefault(key);
        var effectiveSyntax = syntax ?? QuerySyntax.NativeDsl;

        int? ParsePage()
        {
            if (!grouped.TryGetValue(QueryOptionKeys.Page, out var p)) return null;
            if (string.IsNullOrWhiteSpace(p) || !int.TryParse(p, out var page) || page <= 0)
                throw new QueryParseException(QueryOptionKeys.Page, effectiveSyntax, p,
                    new FormatException($"'{p}' is not a valid page number. Page must be a positive integer."));
            return page;
        }

        int? ParsePageSize()
        {
            if (!grouped.TryGetValue(QueryOptionKeys.PageSize, out var ps)) return null;
            if (string.IsNullOrWhiteSpace(ps) || !int.TryParse(ps, out var pageSize) || pageSize <= 0)
                throw new QueryParseException(QueryOptionKeys.PageSize, effectiveSyntax, ps,
                    new FormatException($"'{ps}' is not a valid page size. PageSize must be a positive integer."));
            return pageSize;
        }

        bool? ParseDistinct()
        {
            if (!grouped.TryGetValue(QueryOptionKeys.Distinct, out var dVal)) return null;
            if (dVal.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (dVal.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            throw new QueryParseException(QueryOptionKeys.Distinct, effectiveSyntax, dVal,
                new FormatException($"'{dVal}' is not a valid distinct value. Distinct must be 'true' or 'false'."));
        }

        var parameters = new FlexQueryParameters
        {
            Filter = TryGet(QueryOptionKeys.Filter) ?? TryGet($"${QueryOptionKeys.Filter}"),
            Sort = TryGet(QueryOptionKeys.Sort) ?? TryGet(QueryOptionKeys.OrderBy) ?? TryGet($"${QueryOptionKeys.OrderBy}"),
            Select = TryGet(QueryOptionKeys.Select) ?? TryGet($"${QueryOptionKeys.Select}"),
            Include = TryGet(QueryOptionKeys.Include) ?? TryGet(QueryOptionKeys.Expand) ?? TryGet($"${QueryOptionKeys.Expand}"),
            Expand = TryGet(QueryOptionKeys.Expand) ?? TryGet($"${QueryOptionKeys.Expand}"),
            GroupBy = TryGet(QueryOptionKeys.GroupBy),
            Having = TryGet(QueryOptionKeys.Having),
            Aggregate = TryGet(QueryOptionKeys.Aggregate),
            Page = ParsePage(),
            PageSize = ParsePageSize(),
            Distinct = ParseDistinct(),
            PreserveRawOrder = true
        };

        return Parse(parameters, effectiveSyntax);
    }
}
