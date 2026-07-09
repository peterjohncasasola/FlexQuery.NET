using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Entry point for parsing query-string parameters into unified <see cref="QueryOptions"/>.
/// Supports multiple formats: DSL, JQL, and MiniOData.
/// </summary>
public static class QueryOptionsParser
{
    /// <summary>
    /// The default query syntax used when no per-execution override is supplied.
    /// Set via <c>AddFlexQuery</c> during startup.
    /// </summary>
    private static QuerySyntax _defaultSyntax = QuerySyntax.NativeDsl;

    /// <summary>
    /// Sets the global query syntax used when no per-execution override is supplied.
    /// Called by <c>AddFlexQuery</c> during startup.
    /// </summary>
    internal static void SetGlobalSyntax(QuerySyntax syntax) => _defaultSyntax = syntax;

    /// <summary>
    /// Parses a strongly typed <see cref="FlexQueryParameters"/> into <see cref="QueryOptions"/>.
    /// </summary>
    /// <param name="parameters">The query parameters to parse.</param>
    /// <param name="syntax">The expected query syntax. Defaults to the globally configured syntax (or <see cref="QuerySyntax.NativeDsl"/>).</param>
    /// <returns>The parsed <see cref="QueryOptions"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request parser is unavailable.</exception>
    public static QueryOptions Parse(FlexQueryParameters parameters, QuerySyntax? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var effectiveSyntax = syntax ?? _defaultSyntax;

        var parser = QueryParserRegistry.Resolve(effectiveSyntax);

        var cacheKey = new ParsedQueryCacheKey(
            parameters.Filter, parameters.Sort, parameters.Select,
            parameters.Include, parameters.GroupBy, parameters.Having,
            parameters.Page, parameters.PageSize, parameters.IncludeCount,
            parameters.Distinct, parameters.Mode, parameters.Cursor, parameters.UseKeysetPagination, Version: effectiveSyntax.ToString());

        if (ParserCache.TryGet(cacheKey, out var cached))
        {
            return cached!;
        }

        var parsedOptions = parser.Parse(parameters);
        ParserCache.Set(cacheKey, parsedOptions);

        return parsedOptions;
    }

    /// <summary>
    /// Parses raw query-string key-value pairs into <see cref="QueryOptions"/> using the globally configured syntax.
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
            PreserveRawOrder = true
        };

        return Parse(parameters);
    }
}