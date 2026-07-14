using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Dapper.Options;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Dapper integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryDapper
{
    private static FlexQueryModel? _defaultModel;
    private static DapperQueryOptions _defaultOptions = new();
    private static bool _configured;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the global entity-mapping model configured during application startup.
    /// Used internally by <c>FlexQueryAsync</c> when a per-request model is not supplied.
    /// Returns null if <see cref="Configure"/> has not been called.
    /// </summary>
    internal static FlexQueryModel? DefaultModel
    {
        get
        {
            lock (_lock)
            {
                return _defaultModel;
            }
        }
    }

    /// <summary>
    /// Gets the global Dapper query options configured during application startup.
    /// </summary>
    internal static DapperQueryOptions DefaultOptions
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
    /// Configures the Dapper entity-mapping model used by FlexQuery at runtime.
    /// Must be called once during application startup, before executing any queries.
    /// After the first call, the configuration becomes immutable.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate used to configure FlexQuery Dapper entity mappings.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is attempted after queries have already been executed.</exception>
    public static void Configure(Action<FlexQueryDapperOptions>? configure = null)
    {
        lock (_lock)
        {
            if (_configured)
                throw new InvalidOperationException("FlexQueryDapper has already been configured and is now immutable. Configure must be called before any queries are executed.");

            var options = new FlexQueryDapperOptions();
            configure?.Invoke(options);

            _defaultOptions.UseModel(options.Model.Build());
            _defaultOptions.CommandTimeout = options.CommandTimeout;
            _defaultModel = options.Model.Build();
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
            _defaultModel = null;
            _defaultOptions = new DapperQueryOptions();
            _configured = false;
        }
    }
}
