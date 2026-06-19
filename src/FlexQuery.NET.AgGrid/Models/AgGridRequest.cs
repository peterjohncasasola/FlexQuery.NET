using System.Collections.Generic;

namespace FlexQuery.NET.AgGrid.Models;

public sealed class AgGridRequest
{
    public int StartRow { get; set; }

    public int EndRow { get; set; }

    public Dictionary<string, AgGridFilterNode> FilterModel { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<AgGridSortItem> SortModel { get; set; } = [];

    public List<AgGridGroupColumn>? RowGroupCols { get; set; }
}
