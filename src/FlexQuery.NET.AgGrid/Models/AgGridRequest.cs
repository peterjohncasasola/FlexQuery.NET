namespace FlexQuery.NET.AgGrid.Models;

public sealed class AgGridRequest
{
    public Dictionary<string, AgGridFilterNode> FilterModel { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<AgGridSortItem> SortModel { get; set; } = [];
}
