using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Dapper.Options;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Dapper integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryDapper
{
    /// <summary>
    /// Gets the global entity-mapping model configured during application startup.
    /// Used internally by <c>FlexQueryAsync</c> when a per-request model is not supplied.
    /// </summary>
    internal static FlexQueryModel? DefaultModel { get; set; }

    internal static DapperQueryOptions DefaultOptions { get; set; } = new();

    /// <summary>
    /// Configures the Dapper entity-mapping model used by FlexQuery at runtime.
    /// Must be called once during application startup, before executing any queries.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate used to configure FlexQuery Dapper entity mappings.
    /// </param>
    public static void Configure(Action<FlexQueryDapperOptions>? configure = null)
    {

        var options = new FlexQueryDapperOptions();
        configure?.Invoke(options);

        DefaultOptions.UseModel(options.Model.Build());
        DefaultOptions.CommandTimeout = options.CommandTimeout;
        DefaultModel = options.Model.Build();
    }
}
