using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET;

/// <summary>
/// Provides extension methods for converting <see cref="FlexQueryRequest"/>
/// instances into <see cref="QueryOptions"/> objects.
/// </summary>
public static class FlexQueryRequestExtensions
{
    /// <summary>
    /// Creates a <see cref="QueryOptions"/> instance from the specified
    /// <see cref="FlexQueryRequest"/>.
    /// </summary>
    /// <param name="request">
    /// The FlexQuery request containing the query parameters, typically
    /// deserialized from an HTTP POST request body.
    /// </param>
    /// <returns>
    /// A <see cref="QueryOptions"/> instance containing the equivalent query
    /// configuration represented by the request.
    /// </returns>
    /// <remarks>
    /// This method performs a direct mapping from a <see cref="FlexQueryRequest"/>
    /// to a <see cref="QueryOptions"/> instance. It copies all supported query
    /// components, including:
    /// <list type="bullet">
    /// <item><description>Filter expressions</description></item>
    /// <item><description>Sorting</description></item>
    /// <item><description>Projection (Select)</description></item>
    /// <item><description>Expand / Includes</description></item>
    /// <item><description>Grouping (Group By)</description></item>
    /// <item><description>Aggregate functions</description></item>
    /// <item><description>Having filters</description></item>
    /// <item><description>Paging options</description></item>
    /// <item><description>Distinct</description></item>
    /// <item><description>Projection mode</description></item>
    /// <item><description>Include count</description></item>
    /// </list>
    /// <para>
    /// This extension is useful when a request needs to be inspected, modified,
    /// validated, or executed through APIs that accept a
    /// <see cref="QueryOptions"/> instance.
    /// </para>
    /// </remarks>
    public static QueryOptions ToQueryOptions(this FlexQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var queryOptions = new QueryOptions
        {
            Aggregates = request.Aggregate,
            Filter = request.Filter,
            Distinct = request.Distinct,
            Expand = request.Expand,
            Paging = request.Paging,
            Select = request.Select?.Select(f => new SelectNode { Field = f }).ToList(),
            IncludeCount = request.IncludeCount,
            Includes = request.Include,
            GroupBy = request.GroupBy,
            Sort = request.Sort,
            Having = request.Having,
            ProjectionMode = request.Mode
        };

        return queryOptions;
    }
}