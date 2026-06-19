using System.Text.Json;
using System.Linq;
using FlexQuery.NET.AgGrid.Models;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

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

        // 1. Pagination Support
        if (request.EndRow > request.StartRow && request.StartRow >= 0)
        {
            int pageSize = request.EndRow - request.StartRow;
            if (pageSize > 0)
            {
                result.Paging.PageSize = pageSize;
                result.Paging.Page = (request.StartRow / pageSize) + 1;
            }
        }

        // 2. Row Group Support
        if (request.RowGroupCols is { Count: > 0 })
        {
            var groupByFields = request.RowGroupCols
                .Select(c => c.Field?.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            if (groupByFields.Count > 0)
            {
                result.GroupBy = groupByFields!;
            }
        }

        // 3. Value Columns / Aggregates Support
        if (request.ValueCols is { Count: > 0 })
        {
            foreach (var col in request.ValueCols)
            {
                if (string.IsNullOrEmpty(col.Field) || string.IsNullOrEmpty(col.AggFunc)) continue;

                var fn = col.AggFunc.ToLowerInvariant();
                if (fn == "average") fn = "avg"; // Normalize to canonical

                var alias = ParserUtilities.BuildAggregateAlias(fn, col.Field);
                result.Aggregates.Add(new AggregateModel
                {
                    Field = col.Field,
                    Function = fn,
                    Alias = alias
                });
            }
        }

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

    internal static FilterGroup ParseFilterModel(IReadOnlyDictionary<string, AgGridFilterNode> filterModel)
    {
        return AgGridFilterParser.Parse(filterModel);
    }

    internal static List<SortNode> ParseSortModel(IReadOnlyList<AgGridSortItem> sortModel)
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

        request.StartRow = GetInt(root, "startRow", 0);
        request.EndRow = GetInt(root, "endRow", 0);

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

        if (root.TryGetProperty("rowGroupCols", out var rowGroupCols) && rowGroupCols.ValueKind == JsonValueKind.Array)
        {
            request.RowGroupCols = new List<AgGridGroupColumn>();
            foreach (var item in rowGroupCols.EnumerateArray())
            {
                request.RowGroupCols.Add(new AgGridGroupColumn { Field = GetString(item, "field") });
            }
        }

        if (root.TryGetProperty("valueCols", out var valueCols) && valueCols.ValueKind == JsonValueKind.Array)
        {
            request.ValueCols = new List<AgGridValueColumn>();
            foreach (var item in valueCols.EnumerateArray())
            {
                request.ValueCols.Add(new AgGridValueColumn
                {
                    Field = GetString(item, "field"),
                    AggFunc = GetString(item, "aggFunc")
                });
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

    private static int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? (property.TryGetInt32(out var val) ? val : defaultValue)
            : defaultValue;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        return element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : defaultValue;
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
