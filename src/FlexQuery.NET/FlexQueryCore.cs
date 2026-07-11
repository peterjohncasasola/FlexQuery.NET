using FlexQuery.NET.Configuration;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET;

/// <summary>
/// 
/// </summary>
public static class FlexQueryCore
{
    internal static FlexQueryOptions DefaultOptions { get; set; } = new();

    /// <summary>
    /// Configures the global FlexQuery options that serve as defaults for all queries.
    /// Must be called once during application startup, before any queries are executed.
    /// </summary>
    /// <param name="configure">An optional delegate used to configure the global <see cref="FlexQueryOptions"/>.</param>
    /// <returns>The configured <see cref="FlexQueryOptions"/> instance.</returns>
    public static void Configure(Action<FlexQueryOptions>? configure = null)
    {
        var options = new FlexQueryOptions();
        configure?.Invoke(options);

        QueryOptionsParser.SetGlobalSyntax(options.QuerySyntax);

        DefaultOptions = options;
    }
}