using System.Data.Common;

namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// Resolves the appropriate SQL dialect from the supplied <see cref="DbConnection"/>.
/// This is the single source of truth for SQL dialect selection.
/// </summary>
internal static class SqlDialectResolver
{
    /// <summary>Resolves the appropriate SQL dialect for the given database connection.</summary>
    /// <param name="connection">The database connection to inspect.</param>
    /// <returns>The matching <see cref="ISqlDialect"/> for the connection type.</returns>
    /// <exception cref="NotSupportedException">Thrown when the connection type is not recognized.</exception>
    public static ISqlDialect Resolve(DbConnection connection)
    {
        var typeName = connection.GetType().Name;

        // Check more specific providers first to avoid SqlConnection substring matches.
        if (typeName.EndsWith("NpgsqlConnection", StringComparison.Ordinal))
            return new PostgreSqlDialect();

        if (typeName.EndsWith("SqliteConnection", StringComparison.Ordinal))
            return new SqliteDialect();

        if (typeName.EndsWith("OracleConnection", StringComparison.Ordinal))
            return new OracleDialect();

        if (typeName.EndsWith("MariaDbConnection", StringComparison.Ordinal))
            return new MariaDbDialect();

        if (typeName.EndsWith("MySqlConnection", StringComparison.Ordinal))
            return new MySqlDialect();

        if (typeName.EndsWith("SqlConnection", StringComparison.Ordinal))
            return new SqlServerDialect();

        throw new NotSupportedException(
            $"The connection type '{typeName}' is not a supported database provider. "
            + "Supported providers: SQL Server (SqlConnection), PostgreSQL (NpgsqlConnection), "
            + "SQLite (SqliteConnection), MySQL (MySqlConnection), MariaDB (MariaDbConnection), "
            + "Oracle (OracleConnection). "
            + "To add support for a custom provider, extend SqlDialectResolver.");
    }
}
