using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Extension methods for registering FlexQuery.NET Dapper services in the Microsoft DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures FlexQuery.NET Dapper globally. Registers its services in the DI container, optionally configuring the SQL dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapper(this IServiceCollection services, Action<DapperQueryOptions> configure)
    {
        var options = new DapperQueryOptions();
        configure(options);

        services.AddSingleton(options);
        
        return services;
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected SQL Server dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperSqlServer(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions
        {
            Dialect = new Dialects.SqlServerDialect()
        };
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected PostgreSQL dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperPostgreSql(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions
        {
            Dialect = new Dialects.PostgreSqlDialect()
        };
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected SQLite dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperSqlite(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions
        {
            Dialect = new Dialects.SqliteDialect()
        };
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    private static IServiceCollection AddFlexQueryDapperInternal(this IServiceCollection services, DapperQueryOptions options)
    {
        services.AddSingleton(options);
        
        if (options.Dialect != null)
        {
            services.AddSingleton(options);
        }

        return services;
    }
    
}
