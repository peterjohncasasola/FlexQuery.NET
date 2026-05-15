namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// SQLite dialect implementation.
///
/// SQLite is commonly used for:
/// - Integration testing
/// - Demo APIs
/// - Local development
/// - In-memory testing
///
/// SQLite has specific behaviors:
/// - Identifier escaping uses double quotes (ANSI SQL style)
/// - Parameter prefix uses @ (consistent with Microsoft.Data.Sqlite)
/// - LIMIT/OFFSET pagination (same as MySQL/PostgreSQL)
/// - String concatenation uses the || operator
/// - Boolean literals: 1 for TRUE, 0 for FALSE
/// </summary>
public sealed class SqliteDialect : ISqlDialect
{
    /// <summary>SQLite uses @ parameter prefix with Microsoft.Data.Sqlite.</summary>
    public string ParameterPrefix => "@";

    public string GetCountExpression => "COUNT(1)";

    /// <summary>SQLite does not have native TRUE/FALSE keywords; uses 1 and 0.</summary>
    public string BooleanTrue => "1";
    public string BooleanFalse => "0";

    /// <summary>SQLite uses double-quote identifier escaping (ANSI SQL).</summary>
    public char QuotePrefix => '"';
    public char QuoteSuffix => '"';

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>SQLite uses LIMIT/OFFSET for pagination.</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    /// <summary>SQLite supports LIMIT for top-N queries without OFFSET.</summary>
    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    /// <summary>SQLite uses || operator for string concatenation (same as PostgreSQL).</summary>
    public string Concatenate(params string[] parts)
        => string.Join(" || ", parts);

    public string CreateParameterName(string name) => $"@{name}";
}