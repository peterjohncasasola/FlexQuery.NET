namespace FlexQuery.NET.Adapters.AgGrid.Models;

/// <summary>
/// Intermediate DTO describing a leaf row before it is returned to AG Grid.
/// </summary>
public sealed class AgGridLeafRow
{
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
