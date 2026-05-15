namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Information about a join relationship.
/// </summary>
public sealed class JoinInfo
{
    public string NavigationProperty { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string JoinCondition { get; set; } = string.Empty;
    public Type? TargetType { get; set; }
}
