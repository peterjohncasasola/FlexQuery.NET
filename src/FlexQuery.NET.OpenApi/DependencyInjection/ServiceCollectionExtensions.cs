using FlexQuery.NET.OpenApi;
using FlexQuery.NET.OpenApi.Transformers;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlexQueryOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddFlexQuery());
        return services;
    }
}
