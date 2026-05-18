using Microsoft.Extensions.DependencyInjection;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.MiniOData.Extensions;

/// <summary>
/// Extension methods for registering FlexQuery.NET Mini OData services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Mini OData query parsing support to the service collection.
    /// <para>
    /// This registers the Mini OData query parser as an optional compatibility layer.
    /// It does NOT replace the native FlexQuery DSL parser — both can coexist.
    /// </para>
    /// <example>
    /// <code>
    /// services.AddFlexQuery()
    ///     .AddMiniOData();
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection to add Mini OData support to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddFlexQueryMiniOData(this IServiceCollection services)
    {
        // Register the Mini OData feature flag so middleware/controllers can detect it
        services.AddSingleton<MiniODataFeature>();

        // Register the parser in the central coordinator
        QueryOptionsParser.RegisterParser(new Parsers.MiniODataParser());

        return services;
    }

    /// <summary>
    /// Alias for <see cref="AddFlexQueryMiniOData"/> for cleaner chaining.
    /// </summary>
    public static IServiceCollection AddMiniOData(this IServiceCollection services)
        => services.AddFlexQueryMiniOData();
}

/// <summary>
/// Marker service indicating Mini OData compatibility is enabled.
/// Injected by <see cref="ServiceCollectionExtensions.AddFlexQueryMiniOData"/>.
/// </summary>
public sealed class MiniODataFeature
{
    /// <summary>Whether Mini OData parsing is enabled. Always true when registered.</summary>
    public bool IsEnabled => true;
}
