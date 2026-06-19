namespace FlexQuery.NET.Dapper.Configuration;

public static class DapperQueryOptionsExtensions
{
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

    /// <summary>
    /// Registers the mapping registry with the service collection.
    /// </summary>
    public static DapperQueryOptions UseMappingRegistry(this DapperQueryOptions options, Mapping.IMappingRegistry registry)
    {
        options.MappingRegistry = registry;
        return options;
    }
}
