using FlexQuery.NET.Parsers;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Configuration;

/// <summary>
/// FlexQuery application configuration. In DI applications this is registered as a
/// singleton by <c>AddFlexQuery</c>; non-DI applications can construct it explicitly.
/// Configuration is frozen after the first query execution.
/// </summary>
public sealed class FlexQueryOptions
{
    private bool _frozen;

    /// <summary>The maximum page size that can be requested by clients.</summary>
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>The default page size used when no page size is specified.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Whether to include the total count in query results by default.</summary>
    public bool IncludeTotalCount { get; set; } = true;

    /// <summary>Whether to throw an exception when unauthorized fields are accessed.</summary>
    public bool StrictFieldValidation { get; set; } = true;

    /// <summary>The maximum depth of nested field paths allowed.</summary>
    public int MaxFieldDepth { get; set; } = 5;

    /// <summary>The application-wide default query syntax.</summary>
    public QuerySyntax DefaultQuerySyntax { get; set; } = QuerySyntax.NativeDsl;

    /// <summary>
    /// The application-level mapping registry owned by this options instance.
    /// </summary>
    public IQueryMappingRegistry Registry { get; } = new QueryMappingRegistry();

    /// <summary>
    /// Registers an application-level entity-to-DTO type map.
    /// </summary>
    public ITypeMapExpression<TEntity, TDestination> CreateMap<TEntity, TDestination>()
        where TEntity : class
        where TDestination : class
    {
        ThrowIfFrozen();
        var typeMap = (TypeMap)Registry.GetOrCreate(typeof(TEntity), typeof(TDestination));
        return new TypeMapExpression<TEntity, TDestination>(typeMap);
    }

    /// <summary>
    /// Applies this configuration to a per-query options instance. Existing per-query
    /// values retain precedence over global defaults.
    /// </summary>
    public void ApplyTo(BaseQueryOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfFrozen();

        BaseQueryOptions.ApplyGlobalDefaults(target, this);
        target.GlobalRegistry = Registry;
    }

    /// <summary>
    /// Freezes this configuration. The operation is idempotent.
    /// </summary>
    internal void Freeze()
    {
        if (_frozen)
            return;

        Registry.Freeze();
        _frozen = true;
    }

    internal bool IsFrozen => _frozen;

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("FlexQuery has already executed queries; configuration must happen during startup.");
    }
}
