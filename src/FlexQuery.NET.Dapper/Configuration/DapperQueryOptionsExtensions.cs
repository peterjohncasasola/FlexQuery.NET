namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Extension methods for configuring <see cref="DapperQueryOptions"/> with SQL dialect and mapping registry settings.
/// </summary>
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

}
