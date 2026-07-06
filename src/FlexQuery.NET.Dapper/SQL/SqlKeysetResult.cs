namespace FlexQuery.NET.Dapper.Sql;

internal sealed class SqlKeysetResult
{
    public string WhereClause { get; init; } = string.Empty;
    public string OrderByClause { get; init; } = string.Empty;
}
