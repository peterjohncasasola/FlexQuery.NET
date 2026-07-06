using System.Diagnostics;

namespace FlexQuery.NET.Models;

/// <summary>
/// Wraps paginated query results with metadata.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
[DebuggerDisplay("Page={Page}, PageSize={PageSize}, Count={Data.Count}")]
public sealed class QueryResult<T>
{
    /// <summary>
    /// Total count of matching source records after filtering, before paging and before
    /// cardinality-changing operations such as grouping, distinct projection, or pivoting.
    /// For a normal query this usually matches <see cref="ResultCount"/>. For a grouped
    /// query over 1,432 source rows that produces 4 groups, this value is 1,432.
    /// Existing TotalCount semantics are preserved for backward compatibility.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// Total count of rows produced by the final query shape before paging.
    /// This is useful for grouped, distinct, pivoted, or otherwise shaped queries.
    /// For example, 1,432 source rows grouped by Brand into 4 groups produces
    /// <see cref="TotalCount"/> = 1,432 and <see cref="ResultCount"/> = 4.
    /// For cardinality-preserving queries this usually matches <see cref="TotalCount"/>.
    /// </summary>
    public int? ResultCount { get; init; }

    /// <summary>Current page number.</summary>
    public int Page { get; init; }

    /// <summary>Page size used.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages based on <see cref="ResultCount"/> (falling back to <see cref="TotalCount"/>).</summary>
    public int TotalPages
    {
        get
        {
            var count = ResultCount ?? TotalCount;
            return count.HasValue && PageSize > 0
                ? (int)Math.Ceiling((double)count.Value / PageSize)
                : 0;
        }
    }

    /// <summary>Whether a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Grand total aggregate results (e.g. Salary -> sum -> 1000).</summary>
    public Dictionary<string, Dictionary<string, object>>? Aggregates { get; init; }

    /// <summary>
    /// The rows returned in the current page only. For example, when
    /// <see cref="ResultCount"/> is 100 and the requested page size is 20,
    /// <c>Data.Count</c> is at most 20.
    /// </summary>
    public IReadOnlyList<T> Data { get; init; } = [];

    /// <summary>Serialized cursor token for the next page. Present when keyset pagination was used and there may be more results.</summary>
    public string? NextCursorToken { get; init; }
}
