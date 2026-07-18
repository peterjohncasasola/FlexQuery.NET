using System.Linq.Expressions;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Options;

/// <summary>
/// Infrastructure and customization base for all FlexQuery execution options.
/// This type holds provider-independent, non-governance concerns (shared defaults,
/// listeners, global-default application, and field/expression mapping). Governance
/// (field access control, authorization, validation) lives on
/// <see cref="QueryGovernanceOptions"/>, which derives from this class.
/// </summary>
public abstract class BaseQueryOptions
{
    /// <summary>
    /// Creates a new instance with default infrastructure settings.
    /// </summary>
    protected BaseQueryOptions()
    {
        IncludeTotalCount = true;
        DefaultPageSize = 20;
    }

    // --- Customization / Mapping (translation, not governance) ---

    /// <summary>
    /// Maps a DTO field name to an entity expression for full DTO querying.
    /// </summary>
    public Dictionary<string, LambdaExpression>? ExpressionMappings { get; set; }

    /// <summary>
    /// Maps an exposed DTO field to an entity expression for server-side evaluation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TProperty">The property type of the mapped field.</typeparam>
    /// <param name="alias">The DTO field name (alias) to map.</param>
    /// <param name="expression">An expression that resolves the field from the entity.</param>
    public void MapField<TEntity, TProperty>(string alias, Expression<Func<TEntity, TProperty>> expression)
    {
        ExpressionMappings ??= new Dictionary<string, LambdaExpression>(StringComparer.OrdinalIgnoreCase);
        ExpressionMappings[alias] = expression;
    }

    /// <summary>Maps external field aliases to internal property names.</summary>
    public Dictionary<string, string>? FieldMappings { get; set; }

    // --- Infrastructure defaults ---

    /// <summary>Whether to include the total count in the result by default.</summary>
    public bool IncludeTotalCount { get; set; }

    /// <summary>The default page size to use if not provided by the user.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>The maximum page size a user is allowed to request.</summary>
    public int? MaxPageSize { get; set; }

    /// <summary>If true, field name matching during validation is case-insensitive.</summary>
    public bool CaseInsensitive { get; set; } = true;
    

    /// <summary>
    /// Optional per-request query syntax override.
    /// When <c>null</c>, the global <see cref="Configuration.FlexQueryOptions.DefaultQuerySyntax"/> is used.
    /// </summary>
    public QuerySyntax? QuerySyntax { get; set; }

    /// <summary>
    /// When <c>true</c>, paging is disabled for this request.
    /// Maps to <see cref="Models.Paging.PagingOptions.Disabled"/> during execution.
    /// </summary>
    public bool DisablePaging { get; set; }

    /// <summary>
    /// Optional listener that receives read-only execution events.
    /// The listener is called synchronously within the query pipeline.
    /// Slow listeners will delay query execution.
    /// </summary>
    public IFlexQueryExecutionListener? Listener { get; set; }

    /// <summary>
    /// Applies global application-wide defaults from <see cref="FlexQueryOptions"/>
    /// to a per-request options instance. Values already set on the target are not overridden.
    /// </summary>
    /// <remarks>
    /// This is an infrastructure concern (it may also apply paging/execution/provider defaults),
    /// so it lives on <see cref="BaseQueryOptions"/> even though it copies some governance fields
    /// (inherited from <see cref="QueryGovernanceOptions"/>) when they have not been set.
    /// </remarks>
    internal static void ApplyGlobalDefaults(BaseQueryOptions target, FlexQueryOptions global)
    {
        target.MaxPageSize ??= global.MaxPageSize;

        if (target.DefaultPageSize is 0 or 20)
            target.DefaultPageSize = global.DefaultPageSize;

        if (target.CaseInsensitive)
            target.CaseInsensitive = global.CaseInsensitive;

        if (target.IncludeTotalCount)
            target.IncludeTotalCount = global.IncludeTotalCount;

        // Secure-by-default: strict validation is the effective per-request default
        // (QueryGovernanceOptions.StrictFieldValidation defaults to true) even when these global
        // defaults were never applied. A global lenient policy can still opt the whole application
        // out of strict mode; a per-request 'false' is already lenient and preserved.
        // Governance members live on QueryGovernanceOptions (the runtime type of every concrete
        // options instance); resolve them through the governance base.
        if (target is QueryGovernanceOptions governance)
        {
            if (governance.StrictFieldValidation && !global.StrictFieldValidation)
                governance.StrictFieldValidation = false;

            governance.MaxFieldDepth ??= global.MaxFieldDepth;
        }
    }
}
