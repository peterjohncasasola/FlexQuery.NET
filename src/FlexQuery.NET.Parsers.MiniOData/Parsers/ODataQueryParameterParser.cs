using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers.MiniOData;

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
public static class ODataQueryParameterParser
{
    /// <summary>
    /// Parses OData-style query string parameters into a <see cref="QueryOptions"/>.
    /// Accepts both <c>$filter</c> and <c>filter</c> key formats.
    /// </summary>
    /// <param name="request">Dictionary of query string parameter key-value pairs.</param>
    /// <returns>A <see cref="QueryOptions"/> populated from the OData-style parameters.</returns>
    public static QueryOptions Parse(MiniODataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = new QueryOptions();

        if (!string.IsNullOrWhiteSpace(request.Filter))
            options.Filter = ODataFilterParser.Parse(request.Filter);

        if (!string.IsNullOrWhiteSpace(request.OrderBy))
            options.Sort = ParseOrderBy(request.OrderBy);

        if (!string.IsNullOrWhiteSpace(request.Select))
            options.Select = ParseSelect(request.Select);

        if (!string.IsNullOrWhiteSpace(request.Expand))
            options.Includes = ParseExpand(request.Expand);

        if (request.Top.HasValue)
            options.Paging.PageSize = request.Top.Value;

        if (request is { Skip: not null, Top: not null })
            options.Paging.Page = (request.Skip.Value / request.Top.Value) + 1;

        if (request.Count.HasValue)
            options.IncludeCount = request.Count.Value;

        return options;
    }

    private static List<SortNode> ParseOrderBy(string orderBy)
    {
        var sorts = new List<SortNode>();

        foreach (var segment in orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var field = parts[0].Replace('/', '.');

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

    private static List<string> ParseSelect(string select)
    {
        return select
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.Replace('/', '.'))
            .ToList();
    }

    private static List<string> ParseExpand(string expand)
    {
        return expand
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.Replace('/', '.'))
            .ToList();
    }
}
