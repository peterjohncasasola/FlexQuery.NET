using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Parsers.Fql.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexQuery.NET Fql parser services
/// with the Microsoft dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Fql parser with FlexQuery.
    /// <para>
    /// Registering the parser makes it available for use but does not automatically
    /// activate it.
    /// </para>
    /// <para>
    /// Configure <see cref="QuerySyntax.Fql"/> globally or per execution
    /// to use the Fql parser.
    /// </para>
    /// </summary>
    public static IServiceCollection AddFqlParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        QueryParserRegistry.Register(QuerySyntax.Fql, new FqlQueryParser());
        return services;
    }
}
