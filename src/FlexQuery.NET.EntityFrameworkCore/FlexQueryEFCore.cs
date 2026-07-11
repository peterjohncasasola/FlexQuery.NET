using FlexQuery.NET.EntityFrameworkCore.Configuration;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Entity Framework Core integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryEFCore
{
    /// <summary>
    /// Gets the global EF Core execution options configured during application startup.
    /// Used by <c>FlexQueryAsync</c> when no per-execution options are supplied.
    /// </summary>
    internal static FlexQueryEfCoreOptions? DefaultOptions { get; private set; }

    /// <summary>
    /// Ensures EF Core-specific query operators are registered.
    /// Must be called once during application startup.
    /// </summary>
    public static void Setup()
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();
    }

    /// <summary>
    /// Configures the global EF Core execution options used by FlexQuery at runtime and
    /// ensures EF Core-specific query operators are registered. Must be called once during
    /// application startup, before executing any queries.
    /// </summary>
    /// <param name="configure">
    /// A delegate used to configure the global <see cref="FlexQueryEfCoreOptions"/>.
    /// </param>
    public static void Configure(Action<FlexQueryEfCoreOptions>? configure = null)
    {
        var options = new FlexQueryEfCoreOptions();
        configure?.Invoke(options);
        DefaultOptions = options;
        Setup();
    }
}
