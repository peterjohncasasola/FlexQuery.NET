
using FlexQuery.NET.Builders;

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

    /// <summary>Explicit SQL-style JOIN operations.</summary>
    public List<JoinOption> Joins { get; set; } = new();

    // --- Aliasing ---

    /// <summary>
    /// Tracks the property path (e.g. Left.Left) pointing to the root entity 
    /// after one or more JoinResult wraps.
    /// </summary>
    public string RootAliasPath { get; set; } = string.Empty;

    /// <summary>
    /// Tracks property paths for each explicitly defined join alias.
    /// </summary>
    public Dictionary<string, string> AliasPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Expands a raw field (e.g. "p.amount" or "name") into its full JoinResult path
    /// (e.g. "Left.Right.amount" or "Left.Left.name") if alias routing is active.
    /// </summary>
    public string ExpandFieldAlias(string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return field;

        var parts = field.Split('.', 2);
        if (parts.Length == 2 && AliasPaths.TryGetValue(parts[0], out var aliasPath))
        {
            return $"{aliasPath}.{parts[1]}";
        }
        
        if (!string.IsNullOrEmpty(RootAliasPath))
        {
            return $"{RootAliasPath}.{field}";
        }

        return field;
    }

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
    public bool? IncludeCount { get; set; } = true;

    // --- Execution Configuration ---

    /// <summary>
    /// If true (default), string comparisons will be case-insensitive using database collation.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// If true, enables expression caching for this query.
    /// </summary>
    public bool? EnableCache { get; set; }

    // --- Metadata & Internal State ---

    /// <summary>Custom metadata or items passed through the validation pipeline.</summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>Internal representation of the parsed query for debugging.</summary>
    public object? Ast { get; set; }

    /// <summary>Internal: Merged selection tree from JSON select format.</summary>
    internal SelectionNode? SelectTree { get; set; }

    /// <summary>
    /// Generates a stable cache key for the current query configuration.
    /// This key combines the entity type, operation name, case-sensitivity setting,
    /// and normalized filter structure to produce a deterministic identifier.
    /// </summary>
    /// <param name="entityType">The type of entity being queried.</param>
    /// <param name="operation">The name of the query operation (e.g., "predicate", "projection").</param>
    /// <returns>A string representing the cache key for this query configuration.</returns>
    public string GetCacheKey(Type entityType, string operation)
    {
        var normalizedFilter = FilterNormalizer.Normalize(Filter);
        var filterKey = FilterAnalyzer.CacheKey(normalizedFilter);
        var ciKey = CaseInsensitive ? "ci" : "cs";
        return $"{operation}:{entityType.FullName}:{ciKey}:{filterKey}";
    }

    /// <summary>
    /// Creates a shallow clone of the options with a new filter.
    /// </summary>
    /// <param name="filter">The new filter group to apply.</param>
    /// <returns>A new <see cref="QueryOptions"/> instance with the specified filter and all other properties copied.</returns>
    public QueryOptions CloneWithFilter(FilterGroup? filter)
    {
        var clone = new QueryOptions
        {
            Filter = filter,
            CaseInsensitive = CaseInsensitive,
            EnableCache = EnableCache,
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
            SelectTree = SelectTree,
            RootAliasPath = RootAliasPath
        };

        foreach (var kvp in AliasPaths)
        {
            clone.AliasPaths[kvp.Key] = kvp.Value;
        }

        return clone;
    }
}
