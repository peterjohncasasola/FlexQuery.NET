namespace DynamicQueryable.Models;

/// <summary>
/// Wraps paginated query results with metadata.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class QueryResult<T>
{
    /// <summary>Total count of matching records (before paging).</summary>
    public int TotalCount { get; init; }

    /// <summary>Current page number.</summary>
    public int Page { get; init; }

    /// <summary>Page size used.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Whether a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>The page of data items.</summary>
    public IReadOnlyList<T> Data { get; init; } = [];
}
