using FlexQuery.NET.Security;

namespace FlexQuery.NET.Options;

/// <summary>
/// Provider-independent governance: field access control, authorization, and validation policy.
/// This is the single source of truth for every provider's governance behavior and is the natural
/// target of attribute-based configuration. It derives from <see cref="BaseQueryOptions"/> (which
/// owns infrastructure and customization) and is itself the base for provider-specific options such
/// as <c>EfCoreQueryOptions</c> and <c>DapperQueryOptions</c>.
/// </summary>
public abstract class QueryGovernanceOptions : BaseQueryOptions
{
    /// <summary>
    /// Creates a new instance with default governance settings.
    /// </summary>
    protected QueryGovernanceOptions()
    {
        StrictFieldValidation = true;
    }

    #region Core Governance

    /// <summary>Global list of allowed fields (whitelist).</summary>
    public HashSet<string>? AllowedFields { get; set; }

    /// <summary>Global list of blocked fields (blacklist).</summary>
    public HashSet<string>? BlockedFields { get; set; }

    /// <summary>
    /// Specifies the whitelist of navigation properties that clients are permitted to request
    /// through the <c>include</c> query parameter. This property is used solely for validation
    /// and does not automatically eager-load navigation properties when no <c>include</c> parameter is provided.
    /// </summary>
    public HashSet<string>? AllowedIncludes { get; set; }

    /// <summary>Fields allowed specifically for sorting operations.</summary>
    public HashSet<string>? SortableFields { get; set; }

    /// <summary>Default sort field to use when no sort is specified by the client.</summary>
    public string? DefaultSortField { get; set; }

    /// <summary>Default sort direction when DefaultSortField is used.</summary>
    public bool DefaultSortDescending { get; set; }

    /// <summary>Governance: Map of fields to their explicitly allowed operators (canonical strings).</summary>
    public Dictionary<string, HashSet<string>>? AllowedOperators { get; set; }

    /// <summary>Ergonomic helper to configure allowed operators for a specific field.</summary>
    public void AllowOperators(string field, params string[] operators)
    {
        AllowedOperators ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (!AllowedOperators.TryGetValue(field, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AllowedOperators[field] = set;
        }
        foreach (var op in operators)
        {
            set.Add(Constants.FilterOperators.Normalize(op));
        }
    }

    /// <summary>Limits the depth of nested field paths (e.g. "Customer.Orders.Items").</summary>
    public int? MaxFieldDepth { get; set; }

    /// <summary>
    /// If true, unauthorized field access throws a validation exception.
    /// If false, unauthorized fields are silently removed from the query.
    /// </summary>
    /// <remarks>
    /// Secure-by-default: this is <c>true</c> so that an options instance which is
    /// never passed through <see cref="BaseQueryOptions"/> global defaults still rejects
    /// unauthorized fields instead of silently dropping them. Lenient (silent-removal)
    /// mode is an explicit opt-in: set this to <c>false</c> per request, or configure
    /// <see cref="FlexQueryOptions.StrictFieldValidation"/> to <c>false</c> to opt the
    /// whole application into lenient mode.
    /// </remarks>
    public bool StrictFieldValidation { get; set; } = true;

    #endregion

    #region Granular Governance

    /// <summary>Fields allowed specifically for filtering operations.</summary>
    public HashSet<string>? FilterableFields { get; set; }

    /// <summary>Fields allowed specifically for selection/projection operations.</summary>
    public HashSet<string>? SelectableFields { get; set; }

    /// <summary>Fields allowed specifically for grouping operations.</summary>
    public HashSet<string>? GroupableFields { get; set; }

    /// <summary>Fields allowed specifically for aggregation operations.</summary>
    public HashSet<string>? AggregatableFields { get; set; }

    #endregion

    #region Authorization

    /// <summary>Optional custom resolver for dynamic field-level access control.</summary>
    internal IFieldAccessResolver? FieldAccessResolver { get; set; }

    /// <summary>Role-based field permissions. Maps roles to sets of allowed fields.</summary>
    public Dictionary<string, HashSet<string>>? RoleAllowedFields { get; set; }

    /// <summary>The active role to use when evaluating RoleAllowedFields.</summary>
    public string? CurrentRole { get; set; }

    /// <summary>Optional resolver to dynamically determine allowed fields based on the entity type.</summary>
    public Func<Type, IEnumerable<string>>? AllowedFieldsResolver { get; set; }

    #endregion
}
