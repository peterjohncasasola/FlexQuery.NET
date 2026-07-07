using Microsoft.Extensions.DependencyInjection;
using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering FlexQuery ASP.NET Core components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexQuery field-level security filters to the Mvc options.
    /// </summary>
    /// <param name="builder">The MVC builder to add filters to.</param>
    public static void AddFlexQuerySecurity(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<FieldAccessFilter>();
        });
    }

    /// <summary>
    /// Registers FlexQuery services and global options with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add FlexQuery to.</param>
    /// <param name="configure">Optional configuration action for FlexQueryOptions.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryOptions>? configure = null)
    {
        var options = new FlexQueryOptions();

        configure?.Invoke(options);

        services.AddSingleton(options);

        return services;
    }
}
