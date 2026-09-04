using FlexQuery.NET.Parsers;
using FlexQuery.NET.Mapping;

namespace FlexQuery.NET.Configuration;

/// <summary>
/// Global application-wide FlexQuery defaults.
/// Configured once through DI and acts as the baseline execution behavior for all queries.
/// </summary>
public sealed class FlexQueryOptions
{
    /// <summary>
    /// The maximum page size that can be requested by clients.
    /// </summary>
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>
    /// The default page size used when no page size is specified.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Whether to include the total count in query results by default.
    /// </summary>
    public bool IncludeTotalCount { get; set; } = true;

    /// <summary>
    /// Whether to throw an exception when unauthorized fields are accessed.
    /// </summary>
    public bool StrictFieldValidation { get; set; } = true;

    /// <summary>
    /// The maximum depth of nested field paths allowed.
    /// </summary>
    public int MaxFieldDepth { get; set; } = 5;

    /// <summary>
    /// The application-wide default query syntax used when no per-request override is supplied.
    /// Defaults to <see cref="QuerySyntax.NativeDsl"/>.
    /// </summary>
    public QuerySyntax DefaultQuerySyntax { get; set; } = QuerySyntax.NativeDsl;

    /// <summary>
    /// Registers an application-level entity → DTO type map. Globally registered maps
    /// are automatically reused by every <c>FlexQueryAsync&lt;TEntity, TResponse&gt;</c>
    /// call; same-name compatible properties map by convention, renamed members via
    /// <c>ForMember</c>, and renamed navigation collections via <c>ForNavigation</c>.
    /// Per-query <c>CreateMap</c> registrations take precedence.
    /// </summary>
    /// <example>
    /// <code>
    /// options.CreateMap&lt;Customer, CustomerResponse&gt;()
    ///     .ForMember(dto =&gt; dto.CustomerFullName, entity =&gt; entity.CustomerName);
    /// options.CreateMap&lt;Order, OrderResponse&gt;();
    /// </code>
    /// </example>
    public Mapping.ITypeMapExpression<TEntity, TDestination> CreateMap<TEntity, TDestination>()
        where TEntity : class
        where TDestination : class
    {
        var typeMap = (Mapping.TypeMap)FlexQueryMapping.Registry.GetOrCreate(typeof(TEntity), typeof(TDestination));
        return new Mapping.TypeMapExpression<TEntity, TDestination>(typeMap);
    }
}
