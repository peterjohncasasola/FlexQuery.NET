namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// SQL Server dialect implementation.
/// Supports SQL Server 2012+ with OFFSET/FETCH pagination.
/// Identifier escaping: [Column]
/// Parameter prefix: @
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
{
    public string ParameterPrefix => "@";
    public string GetCountExpression => "COUNT(1)";
    public string BooleanTrue => "1";
    public string BooleanFalse => "0";
    public char QuotePrefix => '[';
    public char QuoteSuffix => ']';

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string GetPagingClause(string offsetParam, string limitParam)
        => $"OFFSET {offsetParam} ROWS FETCH NEXT {limitParam} ROWS ONLY";

    public string GetLimitExpression(string limitParam)
        => $"TOP ({limitParam})";

    public string Concatenate(params string[] parts)
        => string.Join(" + ", parts);

    public string CreateParameterName(string name) => $"@{name}";
}
