
namespace FlexQuery.NET.Models;

/// <summary>
/// Represents the parsed user input for a dynamic query request.
/// This model contains the filtering, sorting, and projection parameters provided by the client.
/// </summary>
public class QueryOptions
{
    // --- Data Selection & Projection ---
    
    /// <summary>The filter expression (JQL or DSL).</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>The sorting expressions.</summary>
    public List<SortNode> Sort { get; set; } = new();

    /// <summary>Flat dot-notation selection paths (e.g. "Id", "Profile.Name").</summary>
    public List<string>? Select { get; set; }

    /// <summary>Navigation properties to include with all scalars.</summary>
    public List<string>? Includes { get; set; }

    /// <summary>Deep, filtered navigation expansion trees.</summary>
    public List<IncludeNode>? FilteredIncludes { get; set; }

    /// <summary>Defines how projected data should be shaped (Nested, Flat, FlatMixed).</summary>
    public ProjectionMode ProjectionMode { get; set; } = ProjectionMode.Nested;

    /// <summary>Fields to group by for aggregation.</summary>
    public List<string>? GroupBy { get; set; }

    /// <summary>Aggregate projection expressions (sum, count, avg).</summary>
    public List<AggregateModel> Aggregates { get; set; } = new();

    /// <summary>HAVING condition against aggregate projections.</summary>
    public HavingCondition? Having { get; set; }

    /// <summary>If true, applies Distinct() to the query.</summary>
    public bool? Distinct { get; set; }

    // --- Pagination ---

    /// <summary>Pagination parameters (Page, PageSize, Disabled).</summary>
    public PagingOptions Paging { get; set; } = new();

    /// <summary>Whether to include the total count in the result.</summary>
    public bool? IncludeCount { get; set; } = true;

    // --- Internal: registration markers (moved from Items dictionary) ---

    /// <summary>Internal: whether EF Core-specific operator handlers are registered for this instance.</summary>
    internal bool UseEfCoreOperators { get; set; }

    // --- Metadata & Internal State ---

    /// <summary>Custom metadata or items passed through the validation pipeline.</summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>Internal: Merged selection tree from JSON select format.</summary>
    internal SelectionNode? SelectTree { get; set; }

    // --- Internal: retained for pipeline use (set via QueryExecutionOptions instead) ---

    /// <summary>Whether string comparisons are case-insensitive.</summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>Whether expression caching is enabled for this query.</summary>
    public bool? EnableCache { get; set; }
}
