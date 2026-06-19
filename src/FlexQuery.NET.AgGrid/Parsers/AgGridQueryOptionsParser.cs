using System.Text.Json;
using FlexQuery.NET.AgGrid.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.AgGrid.Parsers;

public static class AgGridQueryOptionsParser
{
    public static QueryOptions Parse(AgGridRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new QueryOptions();

        if (request.FilterModel.Count > 0)
        {
            result.Filter = AgGridFilterParser.Parse(request.FilterModel);
        }

        result.Sort = AgGridSortParser.Parse(request.SortModel);

        return result;
    }

    public static QueryOptions Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public static QueryOptions Parse(JsonElement json)
    {
        return Parse(DeserializeRequest(json));
    }

    public static FilterGroup ParseFilterModel(IReadOnlyDictionary<string, AgGridFilterNode> filterModel)
    {
        return AgGridFilterParser.Parse(filterModel);
    }

    public static List<SortNode> ParseSortModel(IReadOnlyList<AgGridSortItem> sortModel)
    {
        return AgGridSortParser.Parse(sortModel);
    }

    private static AgGridRequest DeserializeRequest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("AG Grid request JSON must be an object.");
        }

        var request = new AgGridRequest();

        if (root.TryGetProperty("filterModel", out var filterModel) && filterModel.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in filterModel.EnumerateObject())
            {
                request.FilterModel[property.Name] = DeserializeFilterNode(property.Value);
            }
        }

        if (root.TryGetProperty("sortModel", out var sortModel) && sortModel.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sortModel.EnumerateArray())
            {
                request.SortModel.Add(DeserializeSortItem(item));
            }
        }

        return request;
    }

    private static AgGridFilterNode DeserializeFilterNode(JsonElement element)
    {
        var filter = new AgGridFilterNode
        {
            FilterType = GetString(element, "filterType"),
            Type = GetString(element, "type"),
            Operator = GetString(element, "operator"),
            Filter = GetElement(element, "filter"),
            FilterTo = GetElement(element, "filterTo"),
            DateFrom = GetElement(element, "dateFrom"),
            DateTo = GetElement(element, "dateTo")
        };

        if (TryGetArray(element, "values", out var values))
        {
            foreach (var value in values.EnumerateArray())
            {
                filter.Values.Add(value.Clone());
            }
        }

        if (TryGetArray(element, "conditions", out var conditions))
        {
            foreach (var condition in conditions.EnumerateArray())
            {
                filter.Conditions.Add(DeserializeFilterNode(condition));
            }
        }

        return filter;
    }

    private static AgGridSortItem DeserializeSortItem(JsonElement element)
    {
        return new AgGridSortItem
        {
            ColId = GetString(element, "colId"),
            Sort = GetString(element, "sort")
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    private static JsonElement? GetElement(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.Clone()
            : null;
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }
}
