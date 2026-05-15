using FlexQuery.NET.Security;

namespace FlexQuery.NET.Models;

/// <summary>
/// Defines server-side execution rules, validation constraints, and security policies.
/// This model separates server-side requirements from client-side query parameters.
/// </summary>
public class BaseQueryOptions
{

    /// <summary>
    /// Creates a new instance with default security settings.
    /// </summary>
    public BaseQueryOptions()
    {
        // Set default values for execution options
        IncludeTotalCount = true;
        DefaultPageSize = 20;
    }

    // --- Security Lists ---

    /// <summary>Global list of allowed fields (whitelist).</summary>
    public HashSet<string>? AllowedFields { get; set; }

    /// <summary>Global list of blocked fields (blacklist).</summary>
    public HashSet<string>? BlockedFields { get; set; }

    /// <summary>Global list of allowed includes (whitelist for navigation properties).</summary>
    public HashSet<string>? AllowedIncludes { get; set; }

    /// <summary>
    /// Maps a DTO field name to an entity expression for full DTO querying.
    /// </summary>
    public Dictionary<string, System.Linq.Expressions.LambdaExpression>? ExpressionMappings { get; set; }

    /// <summary>
    /// Maps an exposed DTO field to an entity expression for server-side evaluation.
    /// </summary>
    public void MapField<TEntity, TProperty>(string alias, System.Linq.Expressions.Expression<Func<TEntity, TProperty>> expression)
    {
        ExpressionMappings ??= new Dictionary<string, System.Linq.Expressions.LambdaExpression>(StringComparer.OrdinalIgnoreCase);
        ExpressionMappings[alias] = expression;
    }

    /// <summary>
    /// Governance: Map of fields to their explicitly allowed operators (canonical strings).
    /// If a field is not present, all operators are allowed.
    /// Use <see cref="Constants.FilterOperators"/> for valid keys.
    /// </summary>
    public Dictionary<string, HashSet<string>>? AllowedOperators { get; set; }

    /// <summary>
    /// Ergonomic helper to configure allowed operators for a specific field.
    /// Use <see cref="Constants.FilterOperators"/> constants for the operator arguments.
    /// </summary>
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

    /// <summary>Fields allowed specifically for filtering operations.</summary>
    public HashSet<string>? FilterableFields { get; set; }

    /// <summary>Fields allowed specifically for sorting operations.</summary>
    public HashSet<string>? SortableFields { get; set; }

    /// <summary>Fields allowed specifically for selection/projection operations.</summary>
    public HashSet<string>? SelectableFields { get; set; }

    // --- Validation Rules ---

    /// <summary>Limits the depth of nested field paths (e.g. "Customer.Orders.Items").</summary>
    public int? MaxFieldDepth { get; set; }

    /// <summary>
    /// If true, unauthorized field access throws a validation exception.
    /// If false, unauthorized fields are silently removed from the query.
    /// </summary>
    public bool StrictFieldValidation { get; set; }

    /// <summary>Whether to include the total count in the result by default.</summary>
    public bool IncludeTotalCount { get; set; }

    /// <summary>The default page size to use if not provided by the user.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>The maximum page size a user is allowed to request.</summary>
    public int? MaxPageSize { get; set; }

    /// <summary>If true, field name matching during validation is case-insensitive.</summary>
    public bool CaseInsensitiveFields { get; set; } = true;

    /// <summary>Maps external field aliases to internal property names.</summary>
    public Dictionary<string, string>? FieldMappings { get; set; }

    // --- Advanced Security ---

    /// <summary>Optional custom resolver for dynamic field-level access control.</summary>
    public IFieldAccessResolver? FieldAccessResolver { get; set; }

    /// <summary>Role-based field permissions. Maps roles to sets of allowed fields.</summary>
    public Dictionary<string, HashSet<string>>? RoleAllowedFields { get; set; }

    /// <summary>The active role to use when evaluating RoleAllowedFields.</summary>
    public string? CurrentRole { get; set; }

    /// <summary>Optional resolver to dynamically determine allowed fields based on the entity type.</summary>
    public Func<Type, IEnumerable<string>>? AllowedFieldsResolver { get; set; }
}
