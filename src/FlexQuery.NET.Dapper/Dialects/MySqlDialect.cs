namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// MySQL dialect implementation.
/// Identifier escaping: `Column`
/// Parameter prefix: ?
/// </summary>
public sealed class MySqlDialect : ISqlDialect
{
    /// <summary>MySQL parameter prefix, used by MySqlConnector.</summary>
    public string ParameterPrefix => "?";
    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";
    /// <summary>MySQL TRUE literal.</summary>
    public string BooleanTrue => "TRUE";
    /// <summary>MySQL FALSE literal.</summary>
    public string BooleanFalse => "FALSE";
    /// <summary>MySQL identifier quote prefix (backtick).</summary>
    public char QuotePrefix => '`';
    /// <summary>MySQL identifier quote suffix (backtick).</summary>
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
