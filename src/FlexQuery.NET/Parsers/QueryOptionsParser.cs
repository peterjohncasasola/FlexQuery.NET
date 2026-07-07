using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Entry point for parsing query-string parameters into unified <see cref="QueryOptions"/>.
/// Supports multiple formats: Generic, JSON, and DSL.
/// </summary>
public static class QueryOptionsParser
{
    private static List<IQueryParser> _parsers = new()
    {
        new JsonQueryParser(),
        new DslQueryParser()
    };

    static QueryOptionsParser()
    {
        RegisterIfAvailable("FlexQuery.NET.MiniOData.Parsers.MiniODataParser, FlexQuery.NET.Parsers.MiniOData");
        RegisterIfAvailable("FlexQuery.NET.Parsers.Jql.JqlQueryParser, FlexQuery.NET.Parsers.Jql");
    }

    /// <summary>
    /// Registers a new query parser implementation.
    /// New parsers are given priority over existing ones.
    /// Thread-safe: uses copy-on-write so in-progress Parse calls are not affected.
    /// </summary>
    /// <param name="parser">The parser instance to register.</param>
    public static void RegisterParser(IQueryParser parser)
    {
        var updated = new List<IQueryParser>(_parsers);
        updated.Insert(0, parser);
        _parsers = updated;
    }

    private static void RegisterIfAvailable(string typeName)
    {
        try
        {
            if (Type.GetType(typeName) is { } type && Activator.CreateInstance(type) is IQueryParser parser)
                RegisterParser(parser);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Parses a strongly typed <see cref="FlexQueryParameters"/> into <see cref="QueryOptions"/>.
    /// </summary>
    /// <param name="parameters">The query parameters to parse.</param>
    /// <param name="syntax">The expected query syntax. Defaults to AutoDetect.</param>
    /// <returns>The parsed <see cref="QueryOptions"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters"/> is null.</exception>
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
            parameters.Filter, parameters.Sort, parameters.Select,
            parameters.Include, parameters.GroupBy, parameters.Having,
            parameters.Page, parameters.PageSize, parameters.IncludeCount,
            parameters.Distinct, parameters.Mode, parameters.Cursor, parameters.UseKeysetPagination, rawKey, syntax.ToString());

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

        var parsers = _parsers;
        IQueryParser? parser = null;

        if (syntax != QuerySyntax.AutoDetect)
        {
            parser = parsers.FirstOrDefault(p => p.Syntax == syntax);
        }

        parser ??= parsers.FirstOrDefault(p => p.CanParse(parameters)) ?? parsers.Last();

        var parsedOptions = parser.Parse(parameters);
        ParserCache.Set(cacheKey, parsedOptions);

        return parsedOptions;
    }

    /// <summary>
    /// Auto-detects the query-string format and parses into <see cref="QueryOptions"/>.
    /// Resilient: invalid or unrecognized keys are silently ignored.
    /// </summary>
    /// <param name="queryString">The raw query string key-value pairs.</param>
    /// <returns>The parsed <see cref="QueryOptions"/>.</returns>
    public static QueryOptions Parse(IEnumerable<KeyValuePair<string, StringValues>> queryString)
    {
        var grouped = queryString.GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value.ToString(), StringComparer.OrdinalIgnoreCase);

        string? TryGet(string key) => grouped.GetValueOrDefault(key);
        var parameters = new FlexQueryParameters
        {
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
            Cursor = TryGet(QueryOptionKeys.Cursor),
            UseKeysetPagination = grouped.TryGetValue(QueryOptionKeys.UseKeysetPagination, out var ukp) && ukp.Equals("true", StringComparison.OrdinalIgnoreCase),
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

        return string.IsNullOrWhiteSpace(parameters.Filter)
            && IndexedFilterParser.HasIndexedSort(parameters.RawParameters);
    }
    
    private static void TryRegisterParser(string typeName)
    {
        try
        {
            var type = Type.GetType(typeName);

            if (type == null)
                return;

            if (Activator.CreateInstance(type) is IQueryParser parser)
            {
                RegisterParser(parser);
            }
        }
        catch
        {
        }
    }
}
