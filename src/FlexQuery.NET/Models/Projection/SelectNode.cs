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
    public string? Field { get; init; } = string.Empty;

    /// <summary>
    /// Output alias.
    /// Null when omitted.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Nested projections.
    /// </summary>
    public List<SelectNode> Children { get; } = [];

    /// <summary>
    /// True when this node was synthesized by the validator's default-projection or
    /// wildcard-governance expansion rather than written by the API consumer. Expanded
    /// navigation paths are part of the established default/wildcard projection
    /// contract and are exempt from navigation-include authorization — the contract
    /// applies only to explicitly selected navigations.
    /// </summary>
    public bool IsSynthesized { get; init; }
}
