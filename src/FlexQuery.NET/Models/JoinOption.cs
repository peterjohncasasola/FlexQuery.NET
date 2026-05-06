namespace FlexQuery.NET.Models;

/// <summary>
/// Specifies the type of join operation.
/// </summary>
public enum JoinType
{
    Inner,
    Left
}

/// <summary>
/// Represents an explicit SQL-style JOIN operation.
/// </summary>
public class JoinOption
{
    /// <summary>The name/alias of the target collection to join.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>The property path on the left (outer) side of the join.</summary>
    public string LeftKey { get; set; } = string.Empty;

    /// <summary>The property path on the right (inner) side of the join.</summary>
    public string RightKey { get; set; } = string.Empty;

    /// <summary>Optional alias for the joined entity in the resulting projection.</summary>
    public string? Alias { get; set; }

    /// <summary>The type of join to perform (Inner or Left).</summary>
    public JoinType Type { get; set; } = JoinType.Inner;
}
