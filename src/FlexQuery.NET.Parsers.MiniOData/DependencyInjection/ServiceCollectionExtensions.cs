using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.MiniOData;

namespace Microsoft.Extensions.DependencyInjection;

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

        var parser = new MiniODataQueryParser();
        services.AddSingleton<IQueryParser>(parser);
        QueryParserRegistry.Register(QuerySyntax.MiniOData, parser);

        return services;
    }
}
