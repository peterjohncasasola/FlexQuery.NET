namespace DynamicQueryable.Models;

/// <summary>
/// Represents the full set of query options that can be applied to an IQueryable.
/// </summary>
public sealed class QueryOptions
{
    /// <summary>Top-level filter group (supports nested AND/OR logic).</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>Ordered list of sort instructions.</summary>
    public List<SortOption> Sort { get; set; } = [];

    /// <summary>Pagination settings.</summary>
    public PagingOptions Paging { get; set; } = new();

    /// <summary>Fields to project (SELECT). Null or empty means all primitive fields.</summary>
    public List<string>? Select { get; set; }

    /// <summary>Navigation properties to include (Spatie "include").</summary>
    public List<string>? Includes { get; set; }

    /// <summary>Internal tree structure for nested selection.</summary>
    internal SelectionNode? SelectTree { get; set; }
}
