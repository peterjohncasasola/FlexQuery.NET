using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Configures global FlexQuery.NET Dapper services during application startup.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FlexQueryDapperConfigurer"/> is used with
/// <c>AddFlexQueryDapper(...)</c> to configure the default SQL dialect and
/// define the application's entity mapping model.
/// </para>
/// <para>
/// This type is intended for startup configuration only and should not be used
/// during query execution.
/// </para>
/// </remarks>
public sealed class FlexQueryDapperConfigurer
{
    internal FlexQueryDapperConfigurer()
    {
        Model = new ModelBuilder();
    }

    internal ISqlDialect? Dialect { get; private set; }

    /// <summary>
    /// Gets the model builder used to configure entity mappings.
    /// </summary>
    /// <remarks>
    /// Use this builder to configure entities, relationships, and apply
    /// mapping configurations before the model is built for runtime use.
    /// </remarks>
    public ModelBuilder Model { get; }

    /// <summary>
    /// Configures FlexQuery.NET to generate SQL Server-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UseSqlServer()
    {
        Dialect = new SqlServerDialect();
        return this;
    }

    /// <summary>
    /// Configures FlexQuery.NET to generate SQLite-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UseSqlite()
    {
        Dialect = new SqliteDialect();
        return this;
    }

    /// <summary>
    /// Configures FlexQuery.NET to generate PostgreSQL-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UsePostgreSql()
    {
        Dialect = new PostgreSqlDialect();
        return this;
    }

    /// <summary>
    /// Configures FlexQuery.NET to generate MySQL-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UseMySql()
    {
        Dialect = new MySqlDialect();
        return this;
    }

    /// <summary>
    /// Configures FlexQuery.NET to generate MariaDB-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UseMariaDb()
    {
        Dialect = new MariaDbDialect();
        return this;
    }

    /// <summary>
    /// Configures FlexQuery.NET to generate Oracle-compatible queries by default.
    /// </summary>
    /// <returns>The current <see cref="FlexQueryDapperConfigurer"/> instance.</returns>
    public FlexQueryDapperConfigurer UseOracle()
    {
        Dialect = new OracleDialect();
        return this;
    }
}