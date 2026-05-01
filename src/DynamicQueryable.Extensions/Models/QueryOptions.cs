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

    /// <summary>Mode for projection (Nested or Flat).</summary>
    public ProjectionMode ProjectionMode { get; set; } = ProjectionMode.Nested;

    /// <summary>Fields to project (SELECT). Null or empty means all primitive fields.</summary>
    public List<string>? Select { get; set; }

    /// <summary>Fields used for GROUP BY.</summary>
    public List<string>? GroupBy { get; set; }

    /// <summary>Aggregate projections parsed from select=... expressions (sum/count/avg).</summary>
    public List<AggregateModel> Aggregates { get; set; } = [];

    /// <summary>Optional HAVING condition applied after grouping.</summary>
    public HavingCondition? Having { get; set; }

    /// <summary>Navigation properties to include (plain paths, no filters).</summary>
    public List<string>? Includes { get; set; }

    /// <summary>
    /// Structured include trees parsed from the
    /// <c>include=orders(status = Cancelled).orderItems(id = 101)</c> syntax.
    /// Each entry is the root of a depth-first navigation path where every
    /// level can carry its own optional <see cref="IncludeNode.Filter"/>.
    /// This is populated automatically by <see cref="DynamicQueryable.Parsers.QueryOptionsParser"/>
    /// and is consumed by the <c>ApplyFilteredIncludes</c> extension when
    /// using <c>DynamicQueryable.Extensions.EFCore</c>.
    /// </summary>
    public List<IncludeNode> FilteredIncludes { get; set; } = [];

    /// <summary>Internal tree structure for nested selection.</summary>
    internal SelectionNode? SelectTree { get; set; }
}
