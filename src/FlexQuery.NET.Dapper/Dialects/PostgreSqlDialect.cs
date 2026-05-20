namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// PostgreSQL dialect implementation.
/// Identifier escaping: "Column"
/// Parameter prefix: :
/// </summary>
public sealed class PostgreSqlDialect : ISqlDialect
{
    /// <summary>PostgreSQL parameter prefix, used by Npgsql.</summary>
    public string ParameterPrefix => ":";
    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";
    /// <summary>PostgreSQL TRUE literal.</summary>
    public string BooleanTrue => "TRUE";
    /// <summary>PostgreSQL FALSE literal.</summary>
    public string BooleanFalse => "FALSE";
    /// <summary>PostgreSQL identifier quote prefix (double quote).</summary>
    public char QuotePrefix => '"';
    /// <summary>PostgreSQL identifier quote suffix (double quote).</summary>
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
    public string CreateParameterName(string name) => $":{name}";
}
