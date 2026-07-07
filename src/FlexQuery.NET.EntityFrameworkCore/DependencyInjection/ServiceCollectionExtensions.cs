using FlexQuery.NET.EntityFrameworkCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.EntityFrameworkCore.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlexQueryEntityFrameworkCore(
        this IServiceCollection services,
        Action<EfCoreFlexQueryDefaults>? configureDefaults = null)
    {
        var defaults = new EfCoreFlexQueryDefaults();
        configureDefaults?.Invoke(defaults);

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        services.AddSingleton(defaults);

        return services;
    }
}
