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

    /// <summary>Alias for <see cref="Sort"/> to support Sorts naming.</summary>
    public List<SortOption> Sorts
    {
        get => Sort;
        set => Sort = value ?? [];
    }

    /// <summary>Pagination settings.</summary>
    public PagingOptions Paging { get; set; } = new();

    /// <summary>Fields to project (SELECT). Null or empty means all primitive fields.</summary>
    public List<string>? Select { get; set; }

    /// <summary>Fields used for GROUP BY.</summary>
    public List<string>? GroupBy { get; set; }

    /// <summary>Aggregate projections parsed from select=... expressions (sum/count/avg).</summary>
    public List<AggregateModel> Aggregates { get; set; } = [];

    /// <summary>Optional HAVING condition applied after grouping.</summary>
    public HavingCondition? Having { get; set; }

    /// <summary>Navigation properties to include.</summary>
    public List<string>? Includes { get; set; }

    /// <summary>Internal tree structure for nested selection.</summary>
    internal SelectionNode? SelectTree { get; set; }
}
