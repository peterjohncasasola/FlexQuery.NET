using FlexQuery.NET.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Extension methods for registering FlexQuery.NET JQL parser services
/// with the Microsoft dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JQL parser with FlexQuery.
    /// <para>
    /// Registering the parser makes it available for use but does not automatically
    /// activate it.
    /// </para>
    /// <para>
    /// Configure <see cref="QuerySyntax.Jql"/> globally or per execution
    /// to use the JQL parser.
    /// </para>
    /// </summary>
    public static IServiceCollection AddJqlParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        QueryParserRegistry.Register(QuerySyntax.Jql, new JqlQueryParser());
        return services;
    }
}
