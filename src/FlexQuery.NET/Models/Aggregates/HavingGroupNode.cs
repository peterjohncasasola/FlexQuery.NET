namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Parenthesized HAVING group.
/// </summary>
public sealed class HavingGroupNode : HavingNode
{
    /// <summary>Inner expression.</summary>
    public HavingNode Inner { get; set; } = null!;
}
