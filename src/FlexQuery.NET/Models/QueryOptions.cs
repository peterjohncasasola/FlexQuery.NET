
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;

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

    /// <summary>Legacy/Internal: Explicit skip count. Use Paging instead.</summary>
    [Obsolete("Use Paging.Skip instead. Will be removed in v4.0.", error: false)]
    public int? Skip { get; set; }

    /// <summary>Legacy/Internal: Explicit take count. Use Paging.PageSize instead.</summary>
    [Obsolete("Use Paging.PageSize instead. Will be removed in v4.0.", error: false)]
    public int? Top { get; set; }

    /// <summary>
    /// Whether to include the total count in the result.
    /// Configure via <see cref="Models.QueryExecutionOptions.IncludeTotalCount"/> instead.
    /// </summary>
    [Obsolete("Configure via QueryExecutionOptions.IncludeTotalCount instead. Will be removed in a future version.", error: false)]
    public bool? IncludeCount { get; set; } = true;

    // --- Internal: registration markers (moved from Items dictionary) ---

    /// <summary>Internal: whether EF Core-specific operator handlers are registered for this instance.</summary>
    internal bool UseEfCoreOperators { get; set; }

    // --- Metadata & Internal State ---

    /// <summary>Custom metadata or items passed through the validation pipeline.</summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>Internal: Merged selection tree from JSON select format.</summary>
    internal SelectionNode? SelectTree { get; set; }

    // --- Execution Configuration (deprecated — use QueryExecutionOptions instead) ---

    /// <summary>
    /// If true (default), string comparisons will be case-insensitive using database collation.
    /// Configure via <see cref="QueryExecutionOptions.CaseInsensitive"/> instead.
    /// </summary>
    [Obsolete("Configure via QueryExecutionOptions.CaseInsensitive instead. Will be removed in a future version.", error: false)]
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// If true, enables expression caching for this query.
    /// Configure via <see cref="QueryExecutionOptions.EnableCache"/> instead.
    /// </summary>
    [Obsolete("Configure via QueryExecutionOptions.EnableCache instead. Will be removed in a future version.", error: false)]
    public bool? EnableCache { get; set; }

    /// <summary>
    /// Generates a stable cache key for the current query configuration.
    /// Use <see cref="Caching.QueryCacheKeyBuilder.Build(QueryOptions, Type, string)"/> directly instead.
    /// </summary>
    [Obsolete("Use QueryCacheKeyBuilder.Build() directly instead. Will be removed in a future version.", error: false)]
    public string GetCacheKey(Type entityType, string operation)
        => Caching.QueryCacheKeyBuilder.Build(this, entityType, operation);

    /// <summary>
    /// Creates a deep-ish clone of the options to support safe caching.
    /// </summary>
    [Obsolete("Construct a new QueryOptions instead. Will be removed in a future version.", error: false)]
    public QueryOptions Clone()
    {
        var clone = new QueryOptions
        {
            Filter = CloneFilterGroup(Filter),
            Sort = Sort.Select(CloneSort).ToList(),
            Select = Select is null ? null : new List<string>(Select),
            Includes = Includes is null ? null : new List<string>(Includes),
            FilteredIncludes = FilteredIncludes is null ? null : FilteredIncludes.Select(CloneInclude).ToList(),
            ProjectionMode = ProjectionMode,
            GroupBy = GroupBy is null ? null : new List<string>(GroupBy),
            Aggregates = Aggregates.Select(CloneAggregate).ToList(),
            Having = CloneHaving(Having),
            Distinct = Distinct,
            Paging = new PagingOptions { Page = Paging.Page, PageSize = Paging.PageSize, Disabled = Paging.Disabled },
            Skip = Skip,
            Top = Top,
            IncludeCount = IncludeCount,
            CaseInsensitive = CaseInsensitive,
            EnableCache = EnableCache,
            SelectTree = CloneSelection(SelectTree)
        };

        foreach (var kv in Items) clone.Items[kv.Key] = kv.Value;

        return clone;
    }

    /// <summary>
    /// Creates a shallow clone of the options with a new filter.
    /// </summary>
    [Obsolete("Construct a new QueryOptions instead. Will be removed in a future version.", error: false)]
    public QueryOptions CloneWithFilter(FilterGroup? filter)
    {
        return new QueryOptions
        {
            Filter = CloneFilterGroup(filter),
            CaseInsensitive = CaseInsensitive,
            EnableCache = EnableCache,
            Sort = Sort.Select(CloneSort).ToList(),
            Select = Select is null ? null : [..Select],
            Includes = Includes is null ? null : [..Includes],
            FilteredIncludes = FilteredIncludes?.Select(CloneInclude).ToList(),
            ProjectionMode = ProjectionMode,
            GroupBy = GroupBy is null ? null : [..GroupBy],
            Aggregates = Aggregates.Select(CloneAggregate).ToList(),
            Having = CloneHaving(Having),
            Distinct = Distinct,
            Paging = new PagingOptions { Page = Paging.Page, PageSize = Paging.PageSize, Disabled = Paging.Disabled },
            Skip = Skip,
            Top = Top,
            IncludeCount = IncludeCount,
            SelectTree = CloneSelection(SelectTree)
        };
    }

    private static FilterGroup? CloneFilterGroup(FilterGroup? group)
        => group is null
            ? null
            : new FilterGroup
            {
                Logic = group.Logic,
                IsNegated = group.IsNegated,
                Filters = group.Filters.Select(CloneFilterCondition).ToList(),
                Groups = group.Groups.Select(g => CloneFilterGroup(g)!).ToList()
            };

    private static FilterCondition CloneFilterCondition(FilterCondition condition)
        => new()
        {
            Field = condition.Field,
            Operator = condition.Operator,
            Value = condition.Value,
            IsNegated = condition.IsNegated,
            ScopedFilter = CloneFilterGroup(condition.ScopedFilter)
        };

    private static SortNode CloneSort(SortNode sort)
        => new()
        {
            Field = sort.Field,
            Aggregate = sort.Aggregate,
            AggregateField = sort.AggregateField,
            Descending = sort.Descending
        };

    private static IncludeNode CloneInclude(IncludeNode include)
        => new()
        {
            Path = include.Path,
            Filter = CloneFilterGroup(include.Filter),
            Children = include.Children.Select(CloneInclude).ToList()
        };

    private static AggregateModel CloneAggregate(AggregateModel aggregate)
        => new()
        {
            Function = aggregate.Function,
            Field = aggregate.Field,
            Alias = aggregate.Alias
        };

    private static HavingCondition? CloneHaving(HavingCondition? having)
        => having is null
            ? null
            : new HavingCondition
            {
                Function = having.Function,
                Field = having.Field,
                Operator = having.Operator,
                Value = having.Value
            };

    private static SelectionNode? CloneSelection(SelectionNode? node)
    {
        if (node is null) return null;

        var clone = new SelectionNode
        {
            Filter = CloneFilterGroup(node.Filter),
            Alias = node.Alias
        };

        if (node.IncludeAllScalars)
        {
            clone.MarkIncludeAllScalars();
        }

        foreach (var child in node.EnumerateChildren())
        {
            var clonedChild = CloneSelection(child.Value)!;
            var targetChild = clone.GetOrAddChild(child.Key);
            CopySelectionInto(targetChild, clonedChild);
        }

        return clone;
    }

    private static void CopySelectionInto(SelectionNode target, SelectionNode source)
    {
        target.Filter = CloneFilterGroup(source.Filter);
        target.Alias = source.Alias;
        if (source.IncludeAllScalars)
        {
            target.MarkIncludeAllScalars();
        }

        foreach (var child in source.EnumerateChildren())
        {
            var targetChild = target.GetOrAddChild(child.Key);
            CopySelectionInto(targetChild, child.Value);
        }
    }
}
