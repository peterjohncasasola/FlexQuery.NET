using FlexQuery.NET.Configurations;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.DependencyInjection;

/// <summary>
/// Provides dependency injection registration methods for FlexQuery.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core FlexQuery services and optional global configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional delegate used to configure the global
    /// <see cref="FlexQueryOptions"/> instance.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryOptions>? configure = null)
    {
        var options = new FlexQueryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        services.AddSingleton<IFlexQueryProcessor, FlexQueryProcessor>();

        return services;
    }
}
