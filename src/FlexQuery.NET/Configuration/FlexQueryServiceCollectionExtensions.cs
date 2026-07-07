using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Configuration;

public static class FlexQueryServiceCollectionExtensions
{
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryConfig>? configure = null)
    {
        var config = new FlexQueryConfig();
        configure?.Invoke(config);

        services.AddSingleton(config.Options);

        services.AddSingleton<IFlexQueryProcessor>(sp =>
        {
            var options = sp.GetRequiredService<FlexQueryOptions>();
            return new FlexQueryProcessor(options, config.ConfigureExecution);
        });

        return services;
    }
}
