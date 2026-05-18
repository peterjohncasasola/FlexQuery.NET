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
    /// <summary>MariaDB uses ? parameter prefix with MariaDB Connector/NET and MySqlConnector.</summary>
    public string ParameterPrefix => "?";

    public string GetCountExpression => "COUNT(1)";

    /// <summary>MariaDB treats TRUE as 1 and FALSE as 0, but supports TRUE/FALSE keywords in SQL mode.</summary>
    public string BooleanTrue => "TRUE";
    public string BooleanFalse => "FALSE";

    /// <summary>MariaDB uses backtick quoting for identifiers (same as MySQL).</summary>
    public char QuotePrefix => '`';
    public char QuoteSuffix => '`';

    public string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <summary>MariaDB uses the same LIMIT/OFFSET pagination syntax as MySQL.</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    /// <summary>MariaDB supports LIMIT without OFFSET for top-N queries.</summary>
    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    /// <summary>MariaDB uses CONCAT() function for string concatenation.</summary>
    public string Concatenate(params string[] parts)
        => "CONCAT(" + string.Join(", ", parts) + ")";

    public string CreateParameterName(string name) => $"?{name}";
}