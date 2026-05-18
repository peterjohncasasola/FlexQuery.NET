namespace FlexQuery.NET.Dapper.Dialects;

/// <summary>
/// PostgreSQL dialect implementation.
/// Identifier escaping: "Column"
/// Parameter prefix: :
/// </summary>
public sealed class PostgreSqlDialect : ISqlDialect
{
    public string ParameterPrefix => ":";
    public string GetCountExpression => "COUNT(1)";
    public string BooleanTrue => "TRUE";
    public string BooleanFalse => "FALSE";
    public char QuotePrefix => '"';
    public char QuoteSuffix => '"';

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    public string Concatenate(params string[] parts)
        => string.Join(" || ", parts);

    public string CreateParameterName(string name) => $":{name}";
}
