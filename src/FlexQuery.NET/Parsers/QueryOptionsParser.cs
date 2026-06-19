using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Entry point for parsing query-string parameters into unified <see cref="QueryOptions"/>.
/// Supports multiple formats: Generic, JSON, DSL, and JQL.
/// </summary>
public static class QueryOptionsParser
{
    private static readonly List<IQueryParser> _parsers = new()
    {
        new JqlQueryParser(),
        new JsonQueryParser(),
        new DslQueryParser()
    };

    static QueryOptionsParser()
    {
        try
        {
            var odataParserType = Type.GetType("FlexQuery.NET.MiniOData.Parsers.MiniODataParser, FlexQuery.NET.MiniOData");
            if (odataParserType != null)
            {
                var parser = (IQueryParser)Activator.CreateInstance(odataParserType)!;
                RegisterParser(parser);
            }
        }
        catch
        {
            // Ignore if the assembly is not loaded or available
        }
    }

    /// <summary>
    /// Registers a new query parser implementation.
    /// New parsers are given priority over existing ones.
    /// </summary>
    public static void RegisterParser(IQueryParser parser) => _parsers.Insert(0, parser);

    /// <summary>
    /// Parses a strongly typed <see cref="FlexQueryParameters"/> into <see cref="QueryOptions"/>.
    /// </summary>
    public static QueryOptions Parse(FlexQueryParameters parameters, QuerySyntax syntax = QuerySyntax.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        string? rawKey = null;
        if (parameters.RawParameters != null && parameters.RawParameters.Count > 0)
        {
            rawKey = string.Join("&", parameters.RawParameters
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}"));
        }

        var cacheKey = new ParsedQueryCacheKey(
            parameters.Query, parameters.Filter, parameters.Sort, parameters.Select,
            parameters.Include, parameters.GroupBy, parameters.Having,
            parameters.Page, parameters.PageSize, parameters.IncludeCount,
            parameters.Distinct, parameters.Mode, rawKey, syntax.ToString());

        if (ParserCache.TryGet(cacheKey, out var cached))
        {
            return cached!;
        }

        if (ShouldParseGeneric(parameters, syntax))
        {
            var options = IndexedFilterParser.Parse(parameters);
            ParserCache.Set(cacheKey, options);
            return options;
        }

        IQueryParser? parser = null;

        if (syntax != QuerySyntax.AutoDetect)
        {
            parser = _parsers.FirstOrDefault(p => p.Syntax == syntax);
        }

        parser ??= _parsers.FirstOrDefault(p => p.CanParse(parameters)) ?? _parsers.Last();

        var parsedOptions = parser.Parse(parameters);
        ParserCache.Set(cacheKey, parsedOptions);

        return parsedOptions;
    }

    /// <summary>
    /// Auto-detects the query-string format and parses into <see cref="QueryOptions"/>.
    /// Resilient: invalid or unrecognized keys are silently ignored.
    /// </summary>
    public static QueryOptions Parse(IEnumerable<KeyValuePair<string, StringValues>> queryString)
    {
        var grouped = queryString.GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value.ToString(), StringComparer.OrdinalIgnoreCase);

        string? TryGet(string key) => grouped.GetValueOrDefault(key);
        var parameters = new FlexQueryParameters
        {
            Query = TryGet(QueryOptionKeys.Query),
            Filter = TryGet(QueryOptionKeys.Filter) ?? TryGet($"${QueryOptionKeys.Filter}"),
            Sort = TryGet(QueryOptionKeys.Sort) ?? TryGet(QueryOptionKeys.OrderBy) ?? TryGet($"${QueryOptionKeys.OrderBy}"),
            Select = TryGet(QueryOptionKeys.Select) ?? TryGet($"${QueryOptionKeys.Select}"),
            Include = TryGet(QueryOptionKeys.Include) ?? TryGet(QueryOptionKeys.Expand) ?? TryGet($"${QueryOptionKeys.Expand}"),
            GroupBy = TryGet(QueryOptionKeys.Group),
            Having = TryGet(QueryOptionKeys.Having),
            Page = grouped.TryGetValue(QueryOptionKeys.Page, out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = grouped.TryGetValue(QueryOptionKeys.PageSize, out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null,
            IncludeCount = grouped.TryGetValue(QueryOptionKeys.IncludeCount, out var ic) ? ic.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
            Distinct = grouped.TryGetValue(QueryOptionKeys.Distinct, out var dVal) ? dVal.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
            Mode = TryGet(QueryOptionKeys.Mode),
            RawParameters = grouped
        };

        return Parse(parameters);
    }

    private static bool ShouldParseGeneric(FlexQueryParameters parameters, QuerySyntax syntax)
    {
        if (syntax != QuerySyntax.AutoDetect && syntax != QuerySyntax.Generic)
        {
            return false;
        }

        if (IndexedFilterParser.HasIndexFilters(parameters.RawParameters))
        {
            return true;
        }   

        return string.IsNullOrWhiteSpace(parameters.Query)
            && string.IsNullOrWhiteSpace(parameters.Filter)
            && IndexedFilterParser.HasIndexedSort(parameters.RawParameters);
    }
}
