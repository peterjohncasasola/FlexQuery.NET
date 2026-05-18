using System.Data.Common;

namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// Service responsible for automatically resolving the correct SQL dialect from a database connection.
/// </summary>
public interface ISqlDialectResolver
{
    /// <summary>
    /// Resolves the SQL dialect for the given database connection.
    /// </summary>
    ISqlDialect Resolve(DbConnection connection);
}
