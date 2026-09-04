namespace FlexQuery.NET.Mapping;

/// <summary>
/// The application-level mapping registry. Populated once during application startup
/// (via <c>AddFlexQuery</c> / <c>FlexQueryCore.Configure</c>) and automatically
/// consulted by every <c>FlexQueryAsync&lt;TEntity, TResponse&gt;</c> call.
/// Per-query registrations take precedence over global registrations.
/// </summary>
public static class FlexQueryMapping
{
    private static readonly QueryMappingRegistry _registry = new();

    /// <summary>The application-level mapping registry (read-only at query execution time).</summary>
    public static IQueryMappingRegistry Registry => _registry;

    /// <summary>Configures the application-level mapping graph during startup.</summary>
    public static void Configure(Action<IQueryMappingRegistry> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_registry);
    }

    /// <summary>Removes all registrations (infrastructure/test reset).</summary>
    internal static void Reset() => _registry.Clear();
}
