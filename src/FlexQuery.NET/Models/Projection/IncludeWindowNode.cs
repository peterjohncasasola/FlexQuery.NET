using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Hierarchical relationship-query window built from the normalized include tree. The
/// public syntax is flat/path-based (<c>include=Orders(take=3),Orders.OrderItems(take=5)</c>);
/// this node is the internal hierarchical representation consumed by the projection
/// builders so nested collection windows correlate to their already-selected parent.
/// </summary>
internal sealed class IncludeWindowNode
{
    /// <summary>The normalized relationship options for this navigation level.</summary>
    public IncludeNode Node { get; init; } = null!;

    /// <summary>
    /// Child windows keyed by the child navigation property name (entity-level names,
    /// consistent with the include path remainder contract).
    /// </summary>
    public Dictionary<string, IncludeWindowNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
}
