using FlexQuery.NET.EntityFrameworkCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.EntityFrameworkCore.DependencyInjection;

public static class ServiceCollectionExtensions
{
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
