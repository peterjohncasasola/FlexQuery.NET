namespace FlexQuery.NET.Adapters.AgGrid.Models;

/// <summary>
/// SSRM response payload returned to AG Grid via <c>params.success(...)</c>.
/// </summary>
public sealed class AgGridServerSideResponse
{
    /// <summary>
    /// Rows for the current SSRM store request. These may be group rows or leaf rows.
    /// </summary>
    public IReadOnlyList<IDictionary<string, object?>> RowData { get; init; } = [];

    /// <summary>
    /// Total row count for the current level/store. AG Grid uses this for paging and cache sizing.
    /// </summary>
    public int? RowCount { get; init; }
}
