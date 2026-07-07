namespace FlexQuery.NET.Dapper.Sql.Models;

internal sealed class SqlKeysetResult
{
    public string WhereClause { get; init; } = string.Empty;
    public string OrderByClause { get; init; } = string.Empty;
}
