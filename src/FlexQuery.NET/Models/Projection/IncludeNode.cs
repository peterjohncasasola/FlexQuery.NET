using System.Text.Json.Serialization;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Serialization;

namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Represents one level of an included relationship.
/// <para>
/// Produced by parsing the <c>include</c> query option — for example
/// <c>include=orders(filter=status:eq:active;take=5)</c> or a simple
/// <c>include=orders</c>. Each node carries its own optional <see cref="Filter"/>,
/// <see cref="Sort"/>, and <see cref="Take"/> (collection relationships only, enforced
/// by validation) and an ordered list of <see cref="Children"/> representing deeper
/// navigation levels.
/// </para>
/// </summary>
[JsonConverter(typeof(IncludeNodeJsonConverter))]
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

    /// <summary>
    /// Optional sort expressions applied to the navigation collection at this level.
    /// When null or empty, the provider's default ordering is used.
    /// </summary>
    public List<SortNode>? Sort { get; set; }

    /// <summary>
    /// Optional number of items to take from the navigation collection at this level.
    /// Only meaningful for collection navigations.
    /// </summary>
    public int? Take { get; set; }

    /// <summary>
    /// False for intermediate nodes synthesized while expanding a dotted path (e.g. the
    /// <c>orders</c> parent auto-created for the requested path <c>orders.items</c>).
    /// Explicitly requested nodes — written directly in the query or built by the
    /// provider — are true. Governance applies to explicitly-requested paths only; an
    /// implicit parent scaffold is not itself a requested include.
    /// </summary>
    internal bool IsExplicitlyRequested { get; set; } = true;
}
