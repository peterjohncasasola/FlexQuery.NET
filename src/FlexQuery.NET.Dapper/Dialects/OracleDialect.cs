namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// Oracle dialect implementation.
///
/// Oracle Database uses specific SQL syntax:
/// - Identifier escaping with double quotes (uppercased by default)
/// - Parameter prefix: : (named parameters via OracleCommand)
/// - Pagination: OFFSET/FETCH (Oracle 12c+) 
/// - String concatenation with || operator
/// - Boolean handling: Oracle has no native BOOLEAN type in SQL;
///   uses 1/0 or 'Y'/'N' patterns. Oracle does not support TRUE/FALSE
///   keywords in SQL statements.
///
/// NOTE: For Oracle versions prior to 12c, OFFSET/FETCH is not supported.
/// A ROW_NUMBER() based fallback may be needed for legacy Oracle versions.
/// This implementation targets Oracle 12c and later.
/// </summary>
public sealed class OracleDialect : ISqlDialect
{
    /// <summary>Oracle uses : parameter prefix for named parameters.</summary>
    public string ParameterPrefix => ":";

    /// <summary>SQL expression for COUNT.</summary>
    public string GetCountExpression => "COUNT(1)";

    /// <summary>Oracle TRUE literal (uses 1 for booleans).</summary>
    public string BooleanTrue => "1";
    /// <summary>Oracle FALSE literal (uses 0 for booleans).</summary>
    public string BooleanFalse => "0";

    /// <summary>Oracle identifier quote prefix (double quote).</summary>
    public char QuotePrefix => '"';
    /// <summary>Oracle identifier quote suffix (double quote).</summary>
    public char QuoteSuffix => '"';

    /// <summary>Quotes an identifier using double-quote characters.</summary>
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>Oracle 12c+ supports OFFSET/FETCH pagination syntax.</summary>
    public string GetPagingClause(string offsetParam, string limitParam)
        => $"OFFSET {offsetParam} ROWS FETCH NEXT {limitParam} ROWS ONLY";

    /// <summary>Oracle 12c+ supports FETCH FIRST for top-N queries.</summary>
    public string GetLimitExpression(string limitParam)
        => $"FETCH FIRST {limitParam} ROWS ONLY";

    /// <summary>Oracle uses || operator for string concatenation.</summary>
    public string Concatenate(params string[] parts)
        => string.Join(" || ", parts);

    /// <summary>Prepends the parameter prefix to a parameter name.</summary>
    public string CreateParameterName(string name) => $":{name}";
}