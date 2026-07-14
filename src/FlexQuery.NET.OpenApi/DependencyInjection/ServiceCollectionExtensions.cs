using FlexQuery.NET.OpenApi;
using FlexQuery.NET.OpenApi.Transformers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering FlexQuery OpenAPI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FlexQuery OpenAPI integration and configures OpenAPI document
    /// generation to include FlexQuery query parameters and metadata.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> used to register application services.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance so that additional service
    /// registrations can be chained.
    /// </returns>
    public static IServiceCollection AddFlexQueryOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddFlexQuery());
        return services;
    }
}