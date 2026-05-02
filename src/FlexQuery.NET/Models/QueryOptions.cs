using FlexQuery.NET.Security;

namespace FlexQuery.NET.Models;

/// <summary>
/// Configuration options for dynamic querying, including filtering, sorting, pagination, and security.
/// </summary>
public class QueryOptions
{
    // --- Data Selection & Projection ---
    
    /// <summary>The filter expression (JQL or DSL).</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>The sorting expressions.</summary>
    public List<SortOption> Sort { get; set; } = new();

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

    /// <summary>Legacy/Internal: Explicit skip count (if set, overrides Paging.Skip).</summary>
    public int? Skip { get; set; }

    /// <summary>Legacy/Internal: Explicit take count (if set, overrides Paging.PageSize).</summary>
    public int? Top { get; set; }

    /// <summary>Whether to include the total count in the result.</summary>
    public bool? IncludeCount { get; set; }

    // --- Field-Level Security & Aliasing ---

    /// <summary>Global list of allowed fields (whitelist).</summary>
    public HashSet<string>? AllowedFields { get; set; }

    /// <summary>Global list of blocked fields (blacklist).</summary>
    public HashSet<string>? BlockedFields { get; set; }

    /// <summary>Fields allowed specifically for filtering operations.</summary>
    public HashSet<string>? FilterableFields { get; set; }

    /// <summary>Fields allowed specifically for sorting operations.</summary>
    public HashSet<string>? SortableFields { get; set; }

    /// <summary>Fields allowed specifically for selection/projection operations.</summary>
    public HashSet<string>? SelectableFields { get; set; }

    /// <summary>Optional limit for the depth of nested field paths.</summary>
    public int? MaxFieldDepth { get; set; }

    /// <summary>Optional custom resolver for dynamic field-level access control.</summary>
    public IFieldAccessResolver? FieldAccessResolver { get; set; }

    /// <summary>Maps external field aliases to internal property names.</summary>
    public Dictionary<string, string>? FieldMappings { get; set; }

    /// <summary>Role-based field permissions. Maps roles to sets of allowed fields.</summary>
    public Dictionary<string, HashSet<string>>? RoleAllowedFields { get; set; }

    /// <summary>The role to use when evaluating RoleAllowedFields.</summary>
    public string? CurrentRole { get; set; }

    // --- Metadata & Internal State ---

    /// <summary>Custom metadata or items passed through the validation pipeline.</summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>Internal representation of the parsed query for debugging.</summary>
    public object? Ast { get; set; }

    /// <summary>Internal: Merged selection tree from JSON select format.</summary>
    internal SelectionNode? SelectTree { get; set; }

    /// <summary>
    /// If true (default), string comparisons (Contains, StartsWith, EndsWith, Equals)
    /// will be case-insensitive using database collation.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// Creates a shallow clone of the options with a new filter.
    /// </summary>
    public QueryOptions CloneWithFilter(FilterGroup? filter)
    {
        return new QueryOptions
        {
            Filter = filter,
            CaseInsensitive = CaseInsensitive,
            AllowedFields = AllowedFields,
            BlockedFields = BlockedFields,
            FilterableFields = FilterableFields,
            SortableFields = SortableFields,
            SelectableFields = SelectableFields,
            MaxFieldDepth = MaxFieldDepth,
            FieldAccessResolver = FieldAccessResolver,
            FieldMappings = FieldMappings,
            RoleAllowedFields = RoleAllowedFields,
            CurrentRole = CurrentRole,
            Sort = Sort,
            Select = Select,
            Includes = Includes,
            FilteredIncludes = FilteredIncludes,
            ProjectionMode = ProjectionMode,
            GroupBy = GroupBy,
            Aggregates = Aggregates,
            Having = Having,
            Distinct = Distinct,
            Paging = Paging,
            Skip = Skip,
            Top = Top,
            IncludeCount = IncludeCount,
            Ast = Ast,
            SelectTree = SelectTree
        };
    }
}
