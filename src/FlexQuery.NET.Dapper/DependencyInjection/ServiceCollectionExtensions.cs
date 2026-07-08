using FlexQuery.NET.Dapper.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Dapper.DependencyInjection;

/// <summary>
/// Provides dependency injection registration methods for FlexQuery Dapper.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required for FlexQuery Dapper and configures
    /// the metadata model and SQL dialect.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// A delegate used to configure FlexQuery Dapper.
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

        if (configurer.Dialect is not null)
        {
            services.AddSingleton(configurer.Dialect);
        }

        return services;
    }

    /// <summary>
    /// Registers FlexQuery Dapper configured for SQL Server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional delegate used to configure additional Dapper options.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQueryDapperSqlServer(
        this IServiceCollection services,
        Action<FlexQueryDapperOptions>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UseSqlServer();
            configure?.Invoke(cfg);
        });
    }

    /// <summary>
    /// Registers FlexQuery Dapper configured for PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional delegate used to configure additional Dapper options.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQueryDapperPostgreSql(
        this IServiceCollection services,
        Action<FlexQueryDapperOptions>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UsePostgreSql();
            configure?.Invoke(cfg);
        });
    }

    /// <summary>
    /// Registers FlexQuery Dapper configured for SQLite.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional delegate used to configure additional Dapper options.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQueryDapperSqlite(
        this IServiceCollection services,
        Action<FlexQueryDapperOptions>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UseSqlite();
            configure?.Invoke(cfg);
        });
    }
}
