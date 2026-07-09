using FlexQuery.NET.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Parsers.MiniOData.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexQuery.NET Mini OData services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mini OData parser with FlexQuery.
    /// <para>
    /// Registering the parser makes it available for use but does not automatically
    /// activate it.
    /// </para>
    /// <para>
    /// Configure <see cref="QuerySyntax.MiniOData"/> globally or per execution
    /// to use the Mini OData parser.
    /// </para>
    /// </summary>
    public static IServiceCollection AddMiniOData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        QueryParserRegistry.Register(QuerySyntax.MiniOData, new MiniODataQueryParser());
        
        return services;
    }
}
