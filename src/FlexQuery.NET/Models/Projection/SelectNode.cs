namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Strongly-typed representation of a single projection in a SELECT clause.
/// Produced by both DSL and FQL parsers as a normalized AST.
/// </summary>
public sealed class SelectNode
{
    /// <summary>
    /// Property path being projected.
    /// Examples:
    /// Id
    /// Customer.Name
    /// Orders
    /// *
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Output alias.
    /// Null when omitted.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Nested projections.
    /// </summary>
    public List<SelectNode> Children { get; } = [];
}
