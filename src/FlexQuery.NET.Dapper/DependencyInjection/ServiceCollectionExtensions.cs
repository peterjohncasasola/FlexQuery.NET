using FlexQuery.NET.Dapper.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides dependency injection registration methods for FlexQuery Dapper.
/// The SQL dialect is auto-detected from the supplied <see cref="System.Data.Common.DbConnection"/>
/// at runtime — no manual dialect configuration is required.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required for FlexQuery Dapper and configures
    /// the entity mapping model.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// A delegate used to configure FlexQuery Dapper entity mappings.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQueryDapper(
        this IServiceCollection services,
        Action<FlexQueryDapperOptions>? configure = null)
    {
        var configurer = new FlexQueryDapperOptions();
        configure?.Invoke(configurer);

        var model = configurer.Model.Build();
        services.AddSingleton(model);

        return services;
    }
}
