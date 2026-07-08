using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Options;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Provides extension methods for configuring the SQL dialect used by
/// <see cref="DapperQueryOptions"/>.
/// </summary>
public static class DapperQueryOptionsExtensions
{
    /// <summary>
    /// Configures SQL Server as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UseSqlServer(this DapperQueryOptions options)
    {
        options.Dialect = new SqlServerDialect();
        return options;
    }

    /// <summary>
    /// Configures PostgreSQL as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UsePostgreSql(this DapperQueryOptions options)
    {
        options.Dialect = new PostgreSqlDialect();
        return options;
    }

    /// <summary>
    /// Configures SQLite as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UseSqlite(this DapperQueryOptions options)
    {
        options.Dialect = new SqliteDialect();
        return options;
    }

    /// <summary>
    /// Configures MariaDB as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UseMariaDb(this DapperQueryOptions options)
    {
        options.Dialect = new MariaDbDialect();
        return options;
    }

    /// <summary>
    /// Configures MySQL as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UseMySql(this DapperQueryOptions options)
    {
        options.Dialect = new MySqlDialect();
        return options;
    }

    /// <summary>
    /// Configures Oracle Database as the SQL dialect.
    /// </summary>
    /// <param name="options">The Dapper query options.</param>
    /// <returns>The same <see cref="DapperQueryOptions"/> instance.</returns>
    public static DapperQueryOptions UseOracle(this DapperQueryOptions options)
    {
        options.Dialect = new OracleDialect();
        return options;
    }
}