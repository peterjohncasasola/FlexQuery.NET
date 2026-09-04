using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Hierarchical expansion window built from the normalized expand tree. The public
/// expand syntax is flat/path-based (<c>expand=Orders(take=3),Orders.OrderItems(take=5)</c>);
/// this node is the internal hierarchical representation consumed by the projection
/// builders so nested collection windows correlate to their already-selected parent.
/// </summary>
internal sealed class ExpandWindowNode
{
    /// <summary>The normalized expand options for this navigation level.</summary>
    public IncludeNode Node { get; init; } = null!;

    /// <summary>
    /// Child windows keyed by the child navigation property name (entity-level names,
    /// consistent with the expand path remainder contract).
    /// </summary>
    public Dictionary<string, ExpandWindowNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
}
