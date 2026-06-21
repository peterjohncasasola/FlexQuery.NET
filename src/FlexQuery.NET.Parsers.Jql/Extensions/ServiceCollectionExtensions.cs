using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Parsers.Jql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJqlParser(this IServiceCollection services)
    {
        services.AddSingleton<IQueryParser, JqlQueryParser>();
        return services;
    }
}
