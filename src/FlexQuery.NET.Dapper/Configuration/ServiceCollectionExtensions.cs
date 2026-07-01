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

        // Optionally, register the options as a singleton or configured options
        services.AddSingleton(options);
        
        if (options.Dialect != null)
        {
            DapperQueryOptions.GlobalDefaultDialect = options.Dialect;
        }

        // Register dialect as singleton for DI resolution
        services.AddSingleton(DapperQueryOptions.GlobalDefaultDialect ??= options.Dialect!);
        
        // Register mapping registry
        if (options.MappingRegistry != null)
        {
            services.AddSingleton(options.MappingRegistry);
        }

        return services;
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected SQL Server dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperSqlServer(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions();
        options.Dialect = new Dialects.SqlServerDialect();
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected PostgreSQL dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperPostgreSql(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions();
        options.Dialect = new Dialects.PostgreSqlDialect();
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    /// <summary>
    /// Registers FlexQuery.NET Dapper with auto-detected SQLite dialect.
    /// </summary>
    public static IServiceCollection AddFlexQueryDapperSqlite(this IServiceCollection services, Action<DapperQueryOptions>? configure = null)
    {
        var options = new DapperQueryOptions();
        options.Dialect = new Dialects.SqliteDialect();
        configure?.Invoke(options);
        return services.AddFlexQueryDapperInternal(options);
    }

    private static IServiceCollection AddFlexQueryDapperInternal(this IServiceCollection services, DapperQueryOptions options)
    {
        services.AddSingleton(options);
        
        if (options.Dialect != null)
        {
            DapperQueryOptions.GlobalDefaultDialect = options.Dialect;
            services.AddSingleton(options.Dialect);
        }

        if (options.MappingRegistry != null)
        {
            services.AddSingleton(options.MappingRegistry);
        }

        return services;
    }
    
}
