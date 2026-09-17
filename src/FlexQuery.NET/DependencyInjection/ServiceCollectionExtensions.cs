using FlexQuery.NET.Configuration;
using FlexQuery.NET.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency injection registration helpers for FlexQuery.</summary>
public static class FlexQueryServiceCollectionExtensions
{
    /// <summary>
    /// Registers a single application-level <see cref="FlexQueryOptions"/> instance
    /// and its mapping registry. Configuration is executed during service registration.
    /// </summary>
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FlexQueryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IQueryMappingRegistry>(sp =>
            sp.GetRequiredService<FlexQueryOptions>().Registry);

        return services;
    }
}
