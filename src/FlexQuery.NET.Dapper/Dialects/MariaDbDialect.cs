namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// MariaDB dialect implementation.
///
/// MariaDB is NOT a drop-in replacement for MySQL in all scenarios.
/// This dedicated dialect handles MariaDB-specific behavior including:
/// - Identifier escaping with backticks (same as MySQL)
/// - Parameter prefix using ? (same as MySQL Connector/NET)
/// - LIMIT/OFFSET pagination (same as MySQL)
/// - String concatenation with CONCAT()
/// - Boolean literal handling specific to MariaDB
///
/// NOTE: MariaDB has its own versioning, features, and behaviors that may
/// diverge from MySQL. Use this dialect when connecting to MariaDB instances
/// to ensure correct SQL generation for MariaDB-specific edge cases.
/// </summary>
public sealed class MariaDbDialect : ISqlDialect
{
    /// <summary>MariaDB parameter prefix, used by MariaDB Connector/NET and MySqlConnector.</summary>
    public string ParameterPrefix => "?";

    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";

    /// <summary>MariaDB TRUE literal.</summary>
    public string BooleanTrue => "TRUE";
    /// <summary>MariaDB FALSE literal.</summary>
    public string BooleanFalse => "FALSE";

    /// <summary>MariaDB identifier quote prefix.</summary>
    public char QuotePrefix => '`';
    /// <summary>MariaDB identifier quote suffix.</summary>
    public char QuoteSuffix => '`';

    /// <summary>Quotes an identifier using backtick characters.</summary>
    public string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <summary>Generates a LIMIT/OFFSET pagination clause.</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    /// <summary>Generates a LIMIT clause for top-N queries.</summary>
    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    /// <summary>Concatenates expressions using the CONCAT() function.</summary>
    public string Concatenate(params string[] parts)
        => "CONCAT(" + string.Join(", ", parts) + ")";

    /// <summary>Prepends the parameter prefix to a parameter name.</summary>
    public string CreateParameterName(string name) => $"?{name}";

    /// <summary>LIMIT/OFFSET pagination does not require ORDER BY.</summary>
    public bool RequiresOrderByForPaging => false;
}