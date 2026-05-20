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
    /// <summary>SQLite parameter prefix, used by Microsoft.Data.Sqlite.</summary>
    public string ParameterPrefix => "@";
    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";
    /// <summary>SQLite TRUE literal (uses 1 for booleans).</summary>
    public string BooleanTrue => "1";
    /// <summary>SQLite FALSE literal (uses 0 for booleans).</summary>
    public string BooleanFalse => "0";
    /// <summary>SQLite identifier quote prefix (double quote, ANSI SQL style).</summary>
    public char QuotePrefix => '"';
    /// <summary>SQLite identifier quote suffix (double quote, ANSI SQL style).</summary>
    public char QuoteSuffix => '"';

    /// <summary>Quotes an identifier using double-quote characters.</summary>
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>Generates a LIMIT/OFFSET pagination clause.</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    /// <summary>Generates a LIMIT clause for top-N queries.</summary>
    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    /// <summary>Concatenates expressions using the || operator.</summary>
    public string Concatenate(params string[] parts)
        => string.Join(" || ", parts);

    /// <summary>Prepends the parameter prefix to a parameter name.</summary>
    public string CreateParameterName(string name) => $"@{name}";
}