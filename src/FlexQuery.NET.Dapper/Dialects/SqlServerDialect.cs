namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// SQL Server dialect implementation.
/// Supports SQL Server 2012+ with OFFSET/FETCH pagination.
/// Identifier escaping: [Column]
/// Parameter prefix: @
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
{
    /// <summary>SQL Server parameter prefix.</summary>
    public string ParameterPrefix => "@";
    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";
    /// <summary>SQL Server TRUE literal (uses 1 for booleans).</summary>
    public string BooleanTrue => "1";
    /// <summary>SQL Server FALSE literal (uses 0 for booleans).</summary>
    public string BooleanFalse => "0";
    /// <summary>SQL Server identifier quote prefix (square bracket).</summary>
    public char QuotePrefix => '[';
    /// <summary>SQL Server identifier quote suffix (square bracket).</summary>
    public char QuoteSuffix => ']';

    /// <summary>Quotes an identifier using square-bracket characters.</summary>
    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    /// <summary>Generates an OFFSET/FETCH pagination clause (SQL Server 2012+).</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"OFFSET {offsetParam} ROWS FETCH NEXT {limitParam} ROWS ONLY";

    /// <summary>Generates a TOP(N) clause for top-N queries.</summary>
    public string GetLimitExpression(string limitParam)
        => $"TOP ({limitParam})";

    /// <summary>Concatenates expressions using the + operator.</summary>
    public string Concatenate(params string[] parts)
        => string.Join(" + ", parts);

    /// <summary>Prepends the parameter prefix to a parameter name.</summary>
    public string CreateParameterName(string name) => $"@{name}";
}
