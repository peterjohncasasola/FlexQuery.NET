using FlexQuery.NET.EntityFrameworkCore.Configuration;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Entity Framework Core integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryEFCore
{
    private static FlexQueryEfCoreOptions? _defaultOptions;
    private static bool _configured;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the global EF Core execution options configured during application startup.
    /// Used by <c>FlexQueryAsync</c> when no per-execution options are supplied.
    /// Returns null if <see cref="Configure"/> has not been called.
    /// </summary>
    internal static FlexQueryEfCoreOptions? DefaultOptions
    {
        get
        {
            lock (_lock)
            {
                return _defaultOptions;
            }
        }
    }

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
    /// After the first call, the configuration becomes immutable.
    /// </summary>
    /// <param name="configure">
    /// A delegate used to configure the global <see cref="FlexQueryEfCoreOptions"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is attempted after queries have already been executed.</exception>
    public static void Configure(Action<FlexQueryEfCoreOptions>? configure = null)
    {
        lock (_lock)
        {
            if (_configured)
                throw new InvalidOperationException("FlexQueryEFCore has already been configured and is now immutable. Configure must be called before any queries are executed.");

            var options = new FlexQueryEfCoreOptions();
            configure?.Invoke(options);
            _defaultOptions = options;
            _configured = true;
            Setup();
        }
    }

    /// <summary>
    /// Resets the configuration state. For testing only.
    /// </summary>
    internal static void Reset()
    {
        lock (_lock)
        {
            _defaultOptions = null;
            _configured = false;
        }
    }
}
