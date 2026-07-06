namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Information about a join relationship.
/// </summary>
internal sealed class JoinInfo
{
    /// <summary>The navigation property name used for the join.</summary>
    public string NavigationProperty { get; set; } = string.Empty;
    /// <summary>The table name of the joined entity.</summary>
    public string TableName { get; set; } = string.Empty;
    /// <summary>The SQL join condition for this join.</summary>
    public string JoinCondition { get; set; } = string.Empty;
    /// <summary>The target entity type being joined.</summary>
    public Type? TargetType { get; set; }
}
