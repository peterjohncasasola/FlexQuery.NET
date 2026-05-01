namespace DynamicQueryable.Models;

/// <summary>
/// Represents one level of a filtered include path.
/// <para>
/// Produced by parsing a syntax like
/// <c>orders(status = Cancelled).orderItems(id = 101)</c>.
/// Each node carries its own optional <see cref="Filter"/> and an ordered
/// list of <see cref="Children"/> that represent deeper navigation levels.
/// </para>
/// </summary>
public sealed class IncludeNode
{
    /// <summary>Navigation property name at this level (e.g. <c>"orders"</c>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Optional inline filter applied to the navigation collection at this level.
    /// When null the collection is not filtered.
    /// </summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>Deeper navigation levels chained after this one.</summary>
    public List<IncludeNode> Children { get; set; } = [];
}
