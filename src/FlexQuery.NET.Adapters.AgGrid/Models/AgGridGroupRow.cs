namespace FlexQuery.NET.Adapters.AgGrid.Models;

/// <summary>
/// Intermediate DTO describing a server-side group row before it is flattened for JSON output.
/// </summary>
/// <remarks>
/// Its group, key, field, level, leaf-group, route, and child-count values are adapter-defined
/// integration metadata. AG Grid does not require these exact properties in every SSRM payload;
/// applications can consume them through the corresponding client-side callbacks.
/// </remarks>
public sealed class AgGridGroupRow
{
    public string Key { get; init; } = string.Empty;

    public string Field { get; init; } = string.Empty;

    public int Level { get; init; }

    public bool Group { get; init; } = true;

    public bool LeafGroup { get; init; }

    public int? ChildCount { get; init; }

    public IReadOnlyList<string> Route { get; init; } = [];

    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
