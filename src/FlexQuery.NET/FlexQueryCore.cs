using System.Threading;
using System.Threading;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET;

/// <summary>
/// Global FlexQuery configuration entry point.
/// </summary>
public static class FlexQueryCore
{
    private static FlexQueryOptions? _defaultOptions;
    private static bool _configured;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the global FlexQuery options configured during application startup.
    /// Returns an empty options instance if <see cref="Configure"/> has not been called.
    /// </summary>
    internal static FlexQueryOptions DefaultOptions
    {
        get
        {
            lock (_lock)
            {
                if (_defaultOptions is null)
                    _defaultOptions = new FlexQueryOptions();

                return _defaultOptions;
            }
        }
    }

    /// <summary>
    /// Configures the global FlexQuery options that serve as defaults for all queries.
    /// Must be called once during application startup, before executing any queries.
    /// After the first call, the configuration becomes immutable.
    /// </summary>
    /// <param name="configure">An optional delegate used to configure the global <see cref="FlexQueryOptions"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is attempted after queries have already been executed.</exception>
    public static void Configure(Action<FlexQueryOptions>? configure = null)
    {
        lock (_lock)
        {
            if (_configured)
                throw new InvalidOperationException("FlexQueryCore has already been configured and is now immutable. Configure must be called before any queries are executed.");

            var options = new FlexQueryOptions();
            configure?.Invoke(options);

            QueryOptionsParser.SetGlobalSyntax(options.DefaultQuerySyntax);

            _defaultOptions = options;
            _configured = true;
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