using System.Text.Json;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Adapters.AgGrid.Parsers;

internal static class AgGridQueryOptionsParser
{
    public static QueryOptions Parse(AgGridRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new QueryOptions();
        var rowGroupFields = request.RowGroupCols?
            .Select(c => c.Field?.Trim())
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Cast<string>()
            .ToList() ?? [];
        var groupKeyCount = Math.Min(request.GroupKeys.Count, rowGroupFields.Count);
        var isGroupedLevelRequest = rowGroupFields.Count > groupKeyCount;

        if (request.FilterModel.Count > 0)
        {
            result.Filter = AgGridFilterParser.Parse(request.FilterModel);
        }

        result.Sort = ValidateGroupedSorts(
            request.SortModel,
            request.RowGroupCols,
            request.ValueCols,
            rowGroupFields,
            groupKeyCount,
            isGroupedLevelRequest);

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
        if (rowGroupFields.Count > 0)
        {
            if (groupKeyCount > 0)
            {
                result.Filter = MergeFilters(
                    result.Filter,
                    BuildGroupKeyFilter(rowGroupFields, request.GroupKeys, groupKeyCount));
            }

            if (isGroupedLevelRequest)
            {
                result.GroupBy = [rowGroupFields[groupKeyCount]];
            }
        }

        // 3. Value Columns / Aggregates Support
        // AG Grid always sends valueCols for columns configured with aggFunc, even when
        // no grouping is active. Aggregates should only be generated when the request
        // targets an actively grouped level (rowGroupCols.length > groupKeys.length).
        if (request.ValueCols is { Count: > 0 } && isGroupedLevelRequest)
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

    private static List<SortNode> ValidateGroupedSorts(
        IReadOnlyList<AgGridSortItem> sortModel,
        IReadOnlyList<AgGridGroupColumn>? rowGroupCols,
        IReadOnlyList<AgGridValueColumn>? valueCols,
        IReadOnlyList<string> rowGroupFields,
        int groupKeyCount,
        bool isGroupedLevelRequest)
    {
        if (!isGroupedLevelRequest)
        {
            return AgGridSortParser.Parse(sortModel);
        }

        var aggregateLookup = BuildAggregateLookup(valueCols);
        var currentGroupCol = GetCurrentGroupColumn(rowGroupCols, groupKeyCount);
        var currentGroupProjection = currentGroupCol is not null
            ? GroupByBuilder.GetProjectionName(currentGroupCol.Field!)
            : null;

        var resolved = new List<SortNode>(sortModel.Count);
        foreach (var sortItem in sortModel)
        {
            if (string.IsNullOrWhiteSpace(sortItem.ColId)) continue;

            if (aggregateLookup.TryGetValue(sortItem.ColId, out var alias))
            {
                resolved.Add(new SortNode { Field = alias, Descending = IsDescending(sortItem.Sort) });
            }
            else if (currentGroupProjection is not null)
            {
                var colKey = currentGroupCol!.Id ?? currentGroupCol.Field;
                if (string.Equals(sortItem.ColId, colKey, StringComparison.Ordinal))
                {
                    resolved.Add(new SortNode { Field = currentGroupProjection, Descending = IsDescending(sortItem.Sort) });
                }
            }
        }

        var effectiveFallback = currentGroupProjection ?? GroupByBuilder.GetProjectionName(rowGroupFields[groupKeyCount]);
        if (resolved.Count == 0)
        {
            resolved.Add(new SortNode { Field = effectiveFallback });
        }

        return resolved;
    }

    private static AgGridGroupColumn? GetCurrentGroupColumn(IReadOnlyList<AgGridGroupColumn>? rowGroupCols, int groupKeyCount)
    {
        if (rowGroupCols is null || groupKeyCount >= rowGroupCols.Count) return null;
        var col = rowGroupCols[groupKeyCount];
        return string.IsNullOrWhiteSpace(col.Field) ? null : col;
    }

    private static Dictionary<string, string> BuildAggregateLookup(IReadOnlyList<AgGridValueColumn>? valueCols)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        if (valueCols is null) return lookup;

        foreach (var col in valueCols)
        {
            var key = col.Id ?? col.Field;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(col.Field) || string.IsNullOrEmpty(col.AggFunc)) continue;

            var fn = col.AggFunc.ToLowerInvariant();
            if (fn == "average") fn = "avg";
            lookup[key] = ParserUtilities.BuildAggregateAlias(fn, col.Field);
        }

        return lookup;
    }

    private static bool IsDescending(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return false;
        return sort.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);
    }

    private static AgGridRequest DeserializeRequest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("AG Grid request JSON must be an object.");
        }

        var request = new AgGridRequest
        {
            StartRow = GetInt(root, "startRow"),
            EndRow = GetInt(root, "endRow")
        };

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
                request.RowGroupCols.Add(new AgGridGroupColumn { Id = GetString(item, "id"), Field = GetString(item, "field") });
            }
        }

        if (root.TryGetProperty("groupKeys", out var groupKeys) && groupKeys.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in groupKeys.EnumerateArray())
            {
                request.GroupKeys.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : item.ToString());
            }
        }

        if (root.TryGetProperty("valueCols", out var valueCols) && valueCols.ValueKind == JsonValueKind.Array)
        {
            request.ValueCols = new List<AgGridValueColumn>();
            foreach (var item in valueCols.EnumerateArray())
            {
                request.ValueCols.Add(new AgGridValueColumn
                {
                    Id = GetString(item, "id"),
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

    private static FilterGroup BuildGroupKeyFilter(
        IReadOnlyList<string> rowGroupFields,
        IReadOnlyList<string> groupKeys,
        int groupKeyCount)
    {
        var filter = new FilterGroup { Logic = LogicOperator.And };

        for (var i = 0; i < groupKeyCount; i++)
        {
            filter.Filters.Add(new FilterCondition
            {
                Field = rowGroupFields[i],
                Operator = FilterOperators.Equal,
                Value = groupKeys[i]
            });
        }

        return filter;
    }

    private static FilterGroup MergeFilters(FilterGroup? existing, FilterGroup groupKeyFilter)
    {
        if (existing is null)
        {
            return groupKeyFilter;
        }

        var merged = new FilterGroup { Logic = LogicOperator.And };
        merged.Groups.Add(existing);
        merged.Groups.Add(groupKeyFilter);
        return merged;
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
