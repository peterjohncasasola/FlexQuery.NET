namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// Abstraction for SQL dialect-specific behavior.
/// Each dialect encapsulates all provider-specific SQL generation concerns.
/// </summary>
public interface ISqlDialect
{
    /// <summary>SQL parameter prefix (e.g., @ for SQL Server, : for PostgreSQL, ? for MySQL).</summary>
    string ParameterPrefix { get; }

    /// <summary>Wraps an identifier in quotes for the dialect (e.g., [Column], "Column", `Column`).</summary>
    string QuoteIdentifier(string identifier);

    /// <summary>Gets the COUNT expression for count queries.</summary>
    string GetCountExpression { get; }

    /// <summary>Gets the pagination clause (OFFSET/FETCH or LIMIT/OFFSET) for the dialect.</summary>
    string GetPagingClause(string offsetParam, string limitParam);

    /// <summary>Gets the SQL boolean literal for TRUE.</summary>
    string BooleanTrue { get; }

    /// <summary>Gets the SQL boolean literal for FALSE.</summary>
    string BooleanFalse { get; }

    /// <summary>Generates a string concatenation expression for the dialect.</summary>
    string Concatenate(params string[] parts);

    /// <summary>Generates a TOP/N limit expression for the dialect (used when only limit is needed without offset).</summary>
    string GetLimitExpression(string limitParam);

    /// <summary>Quote prefix for identifiers.</summary>
    char QuotePrefix { get; }

    /// <summary>Quote suffix for identifiers.</summary>
    char QuoteSuffix { get; }

    /// <summary>Creates a parameter name with the dialect's parameter prefix.</summary>
    string CreateParameterName(string name);
}
