using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Parsers;

namespace Microsoft.Extensions.DependencyInjection;

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
    /// Registers the core FlexQuery services and optional global configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional delegate used to configure the global
    /// <see cref="FlexQueryOptions"/> instance.
    /// </param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFlexQuery(
        this IServiceCollection services,
        Action<FlexQueryOptions>? configure = null)
    {
        var options = new FlexQueryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        QueryOptionsParser.SetGlobalSyntax(options.QuerySyntax);

        services.AddSingleton<IFlexQueryProcessor, FlexQueryProcessor>();

        return services;
    }
}
