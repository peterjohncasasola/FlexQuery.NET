using FlexQuery.NET.Dapper.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Dapper.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlexQueryDapper(
        this IServiceCollection services,
        Action<FlexQueryDapperConfigurer> configure)
    {
        var configurer = new FlexQueryDapperConfigurer();
        configure(configurer);

        var model = configurer.Model.Build();
        services.AddSingleton(model);

        if (configurer.Dialect is not null)
        {
            services.AddSingleton(configurer.Dialect);
        }

        return services;
    }

    public static IServiceCollection AddFlexQueryDapperSqlServer(
        this IServiceCollection services,
        Action<FlexQueryDapperConfigurer>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UseSqlServer();
            configure?.Invoke(cfg);
        });
    }

    public static IServiceCollection AddFlexQueryDapperPostgreSql(
        this IServiceCollection services,
        Action<FlexQueryDapperConfigurer>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UsePostgreSql();
            configure?.Invoke(cfg);
        });
    }

    public static IServiceCollection AddFlexQueryDapperSqlite(
        this IServiceCollection services,
        Action<FlexQueryDapperConfigurer>? configure = null)
    {
        return services.AddFlexQueryDapper(cfg =>
        {
            cfg.UseSqlite();
            configure?.Invoke(cfg);
        });
    }
}
