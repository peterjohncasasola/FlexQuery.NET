using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.DependencyInjection;

public static class FlexQueryServiceCollectionExtensions
{
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryOptions>? configure = null)
    {
        var options = new FlexQueryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        services.AddSingleton<IFlexQueryProcessor>(sp =>
        {
            var opts = sp.GetRequiredService<FlexQueryOptions>();
            return new FlexQueryProcessor(opts);
        });

        return services;
    }
}
