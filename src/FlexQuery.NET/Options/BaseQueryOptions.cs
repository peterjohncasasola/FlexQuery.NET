using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Resolvers;

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

    // --- AutoMapper-style type-level mapping (preferred API) ---

    private QueryMappingRegistry? _mappingRegistry;

    /// <summary>
    /// The mapping registry for this options instance. Lookups that miss locally fall
    /// through to the application-level registry (<c>FlexQueryMapping</c>), so globally
    /// registered mappings are automatically reused. Per-query registrations win over
    /// global ones. <see cref="QuerySurface"/> is built from this registry.
    /// </summary>
    internal QueryMappingRegistry MappingRegistry
        => _mappingRegistry ??= new QueryMappingRegistry(FlexQueryMapping.Registry);

    /// <summary>
    /// Creates (or returns the existing) per-query type-level mapping between an entity
    /// and a response DTO. Same-name compatible properties map automatically by
    /// convention; use <c>ForMember</c> to register renamed or computed members and
    /// <c>ForNavigation</c> for renamed navigation collections. Per-query registrations
    /// take precedence over globally registered mappings.
    /// </summary>
    /// <example>
    /// <code>
    /// opts.CreateMap&lt;Customer, CustomerResponse&gt;()
    ///     .ForMember(x =&gt; x.CustomerFullName, e =&gt; e.CustomerName)
    ///     .ForMember(x =&gt; x.DiscountPercentage, e =&gt; e.StandardDiscountPercentage);
    /// </code>
    /// </example>
    public ITypeMapExpression<TEntity, TResponse> CreateMap<TEntity, TResponse>()
        where TEntity : class
        where TResponse : class
    {
        var typeMap = (TypeMap)MappingRegistry.GetOrCreate(typeof(TEntity), typeof(TResponse));
        return new TypeMapExpression<TEntity, TResponse>(typeMap);
    }

    // --- Typed DTO mapping (direct property-to-property only, v1) ---

    /// <summary>
    /// Source of truth for typed DTO mapping configuration.
    /// Key = DTO property name; Value = mapping metadata including validated entity property info.
    /// </summary>
    internal Dictionary<string, TypedDtoMapping>? DtoFieldMappings { get; set; }

    /// <summary>
    /// Registers a typed DTO field mapping for direct property-to-property translation.
    /// Both selectors must be direct property accesses; computed expressions are not supported in v1.
    /// </summary>
    /// <typeparam name="TDto">The DTO/response type.</typeparam>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="dtoSelector">Selector for the DTO property (e.g., dto => dto.ContestantName).</param>
    /// <param name="entitySelector">Selector for the entity property (e.g., entity => entity.Name).</param>
    public void MapField<TDto, TEntity, TProperty>(
        Expression<Func<TDto, TProperty>> dtoSelector,
        Expression<Func<TEntity, TProperty>> entitySelector)
        where TDto : class
        where TEntity : class
    {
        var dtoProp = DirectPropertySelector.Extract(dtoSelector, nameof(dtoSelector));
        var entityProp = DirectPropertySelector.Extract(entitySelector, nameof(entitySelector));

        // Canonical registration: the AutoMapper-style mapping registry.
        var typeMap = (TypeMap)MappingRegistry.GetOrCreate(typeof(TEntity), typeof(TDto));
        typeMap.RegisterMember(new PropertyMap
        {
            DestinationName = dtoProp.Name,
            SourceExpression = entitySelector,
            SourceProperty = entityProp,
            DestinationProperty = dtoProp,
            SourceValueType = entityProp.PropertyType,
            DestinationValueType = dtoProp.PropertyType,
            IsImplicit = false,
            IsNavigation = !Metadata.TypeClassification.IsScalarType(entityProp.PropertyType)
        });

        // Legacy mirror kept for backward compatibility with existing internals/tests.
        DtoFieldMappings ??= new Dictionary<string, TypedDtoMapping>(StringComparer.OrdinalIgnoreCase);
        DtoFieldMappings[dtoProp.Name] = new TypedDtoMapping(
            typeof(TDto), dtoProp, typeof(TEntity), entityProp, entitySelector);
    }

    // --- Infrastructure defaults ---

    /// <summary>Whether to include the total count in the result by default.</summary>
    public bool IncludeTotalCount { get; set; }

    /// <summary>The default page size to use if not provided by the user.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>The maximum page size a user is allowed to request.</summary>
    public int? MaxPageSize { get; set; }

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

/// <summary>
/// Metadata for a single typed DTO mapping registered via <see cref="BaseQueryOptions.MapField{TDto, TEntity, TProperty}"/>.
/// </summary>
/// <param name="DtoType">The DTO type the mapping was registered for.</param>
/// <param name="DtoProperty">The DTO property info.</param>
/// <param name="EntityType">The entity type the mapping was registered for.</param>
/// <param name="EntityProperty">The entity property info (validated as direct property access).</param>
/// <param name="EntityExpression">The entity selector expression.</param>
internal sealed record TypedDtoMapping(
    Type DtoType,
    PropertyInfo DtoProperty,
    Type EntityType,
    PropertyInfo EntityProperty,
    LambdaExpression EntityExpression);

