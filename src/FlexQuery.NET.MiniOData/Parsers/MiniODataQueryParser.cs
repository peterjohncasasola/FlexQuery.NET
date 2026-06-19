using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.MiniOData.Parsers;

/// <summary>
/// Parses OData-compatible query parameters into a unified <see cref="QueryOptions"/> object.
/// <para>
/// Supported OData query parameters:
/// <list type="bullet">
///   <item><c>$filter</c> — Filter expression (e.g., <c>name eq 'john'</c>)</item>
///   <item><c>$orderby</c> — Sort expression (e.g., <c>createdAt desc</c>)</item>
///   <item><c>$select</c> — Projection fields (e.g., <c>id,name,email</c>)</item>
///   <item><c>$top</c> — Page size (e.g., <c>10</c>)</item>
///   <item><c>$skip</c> — Skip count (e.g., <c>20</c>)</item>
///   <item><c>$expand</c> — Navigation includes (e.g., <c>orders</c>)</item>
///   <item><c>$count</c> — Include total count (e.g., <c>true</c>)</item>
/// </list>
/// </para>
/// <para>
/// This is a lightweight OData-inspired parser. It does NOT implement the full OData protocol,
/// EDM metadata, batch requests, or delta tracking.
/// </para>
/// </summary>
public static class MiniODataQueryParser
{
    /// <summary>
    /// Parses OData-style query string parameters into a <see cref="QueryOptions"/>.
    /// Accepts both <c>$filter</c> and <c>filter</c> key formats.
    /// </summary>
    /// <param name="queryParams">Dictionary of query string parameter key-value pairs.</param>
    /// <returns>A <see cref="QueryOptions"/> populated from the OData-style parameters.</returns>
    public static QueryOptions Parse(IDictionary<string, string> queryParams)
    {
        ArgumentNullException.ThrowIfNull(queryParams);

        var options = new QueryOptions();
        var normalized = NormalizeKeys(queryParams);

        // $filter
        if (normalized.TryGetValue("filter", out var filterValue) && !string.IsNullOrWhiteSpace(filterValue))
        {
            options.Filter = ODataFilterParser.Parse(filterValue);
        }

        // $orderby
        if (normalized.TryGetValue("orderby", out var orderByValue) && !string.IsNullOrWhiteSpace(orderByValue))
        {
            options.Sort = ParseOrderBy(orderByValue);
        }

        // $select
        if (normalized.TryGetValue("select", out var selectValue) && !string.IsNullOrWhiteSpace(selectValue))
        {
            options.Select = ParseSelect(selectValue);
        }

        // $top
        if (normalized.TryGetValue("top", out var topValue) && int.TryParse(topValue, out var top))
        {
            options.Paging.PageSize = top;
            options.Top = top;
        }

        // $skip
        if (normalized.TryGetValue("skip", out var skipValue) && int.TryParse(skipValue, out var skip))
        {
            options.Skip = skip;
            // Convert skip + top to page number if top is available
            if (options.Top.HasValue && options.Top.Value > 0)
            {
                options.Paging.Page = (skip / options.Top.Value) + 1;
            }
        }

        // $expand
        if (normalized.TryGetValue("expand", out var expandValue) && !string.IsNullOrWhiteSpace(expandValue))
        {
            options.Includes = ParseExpand(expandValue);
        }

        // $count
        if (normalized.TryGetValue("count", out var countValue))
        {
            if (bool.TryParse(countValue, out var includeCount))
            {
                options.IncludeCount = includeCount;
            }
        }

        return options;
    }

    /// <summary>
    /// Parses OData-style query string from <see cref="StringValues"/> (ASP.NET Core compatible).
    /// </summary>
    public static QueryOptions Parse(IDictionary<string, StringValues> queryParams)
    {
        ArgumentNullException.ThrowIfNull(queryParams);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in queryParams)
        {
            var value = kv.Value.ToString();
            if (!string.IsNullOrEmpty(value))
                dict[kv.Key] = value;
        }

        return Parse(dict);
    }

    // ── OrderBy Parsing ─────────────────────────────────────────────────

    private static List<SortNode> ParseOrderBy(string orderBy)
    {
        var sorts = new List<SortNode>();

        foreach (var segment in orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var field = parts[0].Replace('/', '.'); // OData uses / for path separators

            var descending = parts.Length > 1
                             && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            sorts.Add(new SortNode
            {
                Field = field,
                Descending = descending
            });
        }

        return sorts;
    }

    // ── Select Parsing ──────────────────────────────────────────────────

    private static List<string> ParseSelect(string select)
    {
        return select
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.Replace('/', '.'))
            .ToList();
    }

    // ── Expand Parsing ──────────────────────────────────────────────────

    private static List<string> ParseExpand(string expand)
    {
        return expand
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.Replace('/', '.'))
            .ToList();
    }

    // ── Key Normalization ───────────────────────────────────────────────

    /// <summary>
    /// Normalizes query parameter keys by stripping the <c>$</c> prefix
    /// and converting to lowercase for consistent lookup.
    /// </summary>
    private static Dictionary<string, string> NormalizeKeys(IDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in source)
        {
            var key = kv.Key.TrimStart('$').Trim();
            if (!string.IsNullOrEmpty(key))
                result[key] = kv.Value;
        }

        return result;
    }
}
