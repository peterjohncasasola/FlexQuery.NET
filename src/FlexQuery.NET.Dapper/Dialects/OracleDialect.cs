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

    public string GetCountExpression => "COUNT(1)";

    /// <summary>Oracle does not have native TRUE/FALSE in SQL; uses 1 and 0.</summary>
    public string BooleanTrue => "1";
    public string BooleanFalse => "0";

    /// <summary>Oracle uses double-quote identifier escaping; identifiers are case-sensitive when quoted.</summary>
    public char QuotePrefix => '"';
    public char QuoteSuffix => '"';

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

    public string CreateParameterName(string name) => $":{name}";
}