using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
namespace FlexQuery.NET.Models;

/// <summary>
/// The standard request model for FlexQuery.NET.
/// Represents a query sent in the body of an HTTP POST request.
/// </summary>
public sealed class FlexQueryRequest
{
    // --- Data Selection & Projection ---
    
    /// <summary>The filter expression (FQL or DSL).</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>The sorting expressions.</summary>
    public List<SortNode> Sort { get; set; } = [];

    /// <summary>Flat dot-notation selection paths (e.g. "Id", "Profile.Name").</summary>
    public List<string>? Select { get; set; }

    /// <summary>Navigation properties to include with all scalars.</summary>
    public List<string>? Include { get; set; }

    /// <summary>Deep, filtered navigation expansion trees.</summary>
    public List<IncludeNode>? Expand { get; set; }

    /// <summary>Defines how projected data should be shaped (Nested, Flat, FlatMixed).</summary>
    public ProjectionMode Mode { get; set; } = ProjectionMode.Nested;

    /// <summary>Fields to group by for aggregation.</summary>
    public List<string>? GroupBy { get; set; }

    /// <summary>Aggregate projection expressions (sum, count, avg).</summary>
    public List<AggregateModel> Aggregate { get; set; } = [];

    /// <summary>HAVING condition against aggregate projections.</summary>
    public HavingCondition? Having { get; set; }

    /// <summary>If true, applies Distinct() to the query.</summary>
    public bool? Distinct { get; set; }

    // --- Pagination ---

    /// <summary>Pagination parameters (Page, PageSize, Disabled).</summary>
    public PagingOptions Paging { get; set; } = new();

    /// <summary>Whether to include the total count in the result.</summary>
    public bool? IncludeCount { get; set; } = true;
}