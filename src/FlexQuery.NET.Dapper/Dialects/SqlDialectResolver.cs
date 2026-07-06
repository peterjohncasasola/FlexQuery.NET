using System.Data.Common;

namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// Default implementation of ISqlDialectResolver that inspects the connection type name.
/// </summary>
internal static class SqlDialectResolver
{
    /// <summary>Resolves the appropriate SQL dialect for the given database connection.</summary>
    public static ISqlDialect Resolve(DbConnection connection)
    {
        var typeName = connection.GetType().Name;

        if (typeName.Contains("NpgsqlConnection", StringComparison.OrdinalIgnoreCase))
            return new PostgreSqlDialect();
            
        if (typeName.Contains("SqliteConnection", StringComparison.OrdinalIgnoreCase))
            return new SqliteDialect();
            
        if (typeName.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase))
            return new OracleDialect();
            
        // MariaDB Connector/NET uses MySqlConnection or sometimes MariaDbConnection depending on the library
        if (typeName.Contains("MariaDbConnection", StringComparison.OrdinalIgnoreCase))
            return new MariaDbDialect();
            
        if (typeName.Contains("MySqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            // Optional: You could inspect connection.ConnectionString for "MariaDB" if needed, 
            // but returning MySqlDialect is safe as a baseline for MySqlConnection.
            return new MySqlDialect(); 
        }

        // Fallback or explicit SqlConnection
        return new SqlServerDialect();
    }
}
