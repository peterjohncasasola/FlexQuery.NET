using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Extension methods for registering FlexQuery.NET JQL parser services
/// with the Microsoft dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="JqlQueryParser"/> as a singleton <see cref="IQueryParser"/>
    /// service in the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add the JQL parser to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    public static IServiceCollection AddJqlParser(this IServiceCollection services)
    {
        services.AddSingleton<IQueryParser, JqlQueryParser>();
        return services;
    }
}
