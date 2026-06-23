using System.Collections.Generic;

namespace FlexQuery.NET.Adapters.AgGrid.Models;

public sealed class AgGridRequest
{
    public int StartRow { get; set; }

    public int EndRow { get; set; }

    public Dictionary<string, AgGridFilterNode> FilterModel { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<AgGridSortItem> SortModel { get; set; } = [];

    public List<AgGridGroupColumn>? RowGroupCols { get; set; }

    public List<string> GroupKeys { get; set; } = [];

    public List<AgGridValueColumn>? ValueCols { get; set; }
}
