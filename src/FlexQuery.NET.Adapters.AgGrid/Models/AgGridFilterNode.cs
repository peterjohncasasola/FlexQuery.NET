using System.Text.Json;

namespace FlexQuery.NET.Adapters.AgGrid.Models;

public sealed class AgGridFilterNode
{
    public string? FilterType { get; set; }

    public string? Type { get; set; }

    public string? Operator { get; set; }

    public JsonElement? Filter { get; set; }

    public JsonElement? FilterTo { get; set; }

    public JsonElement? DateFrom { get; set; }

    public JsonElement? DateTo { get; set; }

    public List<JsonElement> Values { get; set; } = [];

    public List<AgGridFilterNode> Conditions { get; set; } = [];
}
