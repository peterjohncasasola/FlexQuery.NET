using FlexQuery.NET.EntityFrameworkCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexQuery Entity Framework Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required to use FlexQuery with Entity Framework Core
    /// and configures the default EF Core execution options.
    /// </summary>
    /// <param name="services">
    /// The service collection to add the FlexQuery Entity Framework Core services to.
    /// </param>
    /// <param name="configureDefaults">
    /// An optional delegate used to configure the default
    /// <see cref="FlexQueryEfCoreOptions"/> instance that will be registered as a singleton.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddFlexQueryEntityFrameworkCore(
        this IServiceCollection services,
        Action<FlexQueryEfCoreOptions>? configureDefaults = null)
    {
        var defaults = new FlexQueryEfCoreOptions();
        configureDefaults?.Invoke(defaults);

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        services.AddSingleton(defaults);

        return services;
    }
}
