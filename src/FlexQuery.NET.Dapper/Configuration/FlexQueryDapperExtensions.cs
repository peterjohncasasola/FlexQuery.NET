using Microsoft.Extensions.DependencyInjection;
using FlexQuery.NET.Dapper.Sql;

namespace FlexQuery.NET.Dapper.Configuration;

public static class FlexQueryDapperExtensions
{
    /// <summary>
    /// Configures FlexQuery.NET Dapper globally.
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

        return services;
    }

    /// <summary>
    /// Configures the SQL Server dialect.
    /// </summary>
    public static DapperQueryOptions UseSqlServer(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.SqlServerDialect();
        return options;
    }

    /// <summary>
    /// Configures the PostgreSQL dialect.
    /// </summary>
    public static DapperQueryOptions UsePostgreSql(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.PostgreSqlDialect();
        return options;
    }

    /// <summary>
    /// Configures the SQLite dialect.
    /// </summary>
    public static DapperQueryOptions UseSqlite(this DapperQueryOptions options)
    {
        options.Dialect = new Dialects.SqliteDialect();
        return options;
    }
}
