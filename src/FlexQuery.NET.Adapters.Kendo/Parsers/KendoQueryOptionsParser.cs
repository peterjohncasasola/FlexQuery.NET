using System.Text.Json;
using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Adapters.Kendo.Parsers;

/// <summary>
/// Parses Kendo UI DataSource requests into FlexQuery.NET QueryOptions.
/// </summary>
public static class KendoQueryOptionsParser
{
    /// <summary>
    /// Parses a Kendo UI DataSource request into FlexQuery.NET QueryOptions.
    /// </summary>
    /// <param name="request">The Kendo UI DataSource request.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    public static QueryOptions Parse(KendoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new QueryOptions();

        // Parse filters
        if (request.Filter != null)
        {
            result.Filter = KendoFilterParser.Parse(request.Filter);
        }

        // Parse sorts
        result.Sort = KendoSortParser.Parse(request.Sort);

        // Handle pagination - Kendo supports multiple pagination modes
        if (request.Take > 0)
        {
            result.Paging.PageSize = request.Take;
            
            if (request.Skip >= 0)
            {
                // Calculate page from skip and take
                result.Paging.Page = (request.Skip / request.Take) + 1;
            }
            else if (request.Page > 0)
            {
                // Use explicit page number
                result.Paging.Page = request.Page;
            }
            else
            {
                // Default to first page
                result.Paging.Page = 1;
            }
        }
        else if (request.PageSize > 0 && request.Page > 0)
        {
            // Alternative pagination mode using page and pageSize
            result.Paging.PageSize = request.PageSize;
            result.Paging.Page = request.Page;
        }

        // Handle groups
        if (request.Group != null && request.Group.Count > 0)
        {
            var groupByFields = request.Group
                .Select(g => g.Field?.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            if (groupByFields.Count > 0)
            {
                result.GroupBy = groupByFields!;
                
                // Process group-level aggregates
                ProcessGroupAggregates(request.Group, result);
            }
        }

        // Handle top-level aggregates
        if (request.Aggregates != null && request.Aggregates.Count > 0)
        {
            foreach (var aggregate in request.Aggregates)
            {
                AddAggregate(result, aggregate.Field, aggregate.Aggregate);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a JSON string containing a Kendo UI DataSource request.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    /// <summary>
    /// Parses a JsonElement containing a Kendo UI DataSource request.
    /// </summary>
    /// <param name="json">The JsonElement to parse.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions Parse(JsonElement json)
    {
        return Parse(DeserializeRequest(json));
    }

    /// <summary>
    /// Parses a Kendo UI filter into a FlexQuery.NET filter group.
    /// </summary>
    /// <param name="filter">The Kendo UI filter to parse.</param>
    /// <returns>A FlexQuery.NET filter group, or null if the filter is empty.</returns>
    internal static FilterGroup? ParseFilter(KendoFilter? filter)
    {
        return KendoFilterParser.Parse(filter);
    }

    /// <summary>
    /// Parses Kendo UI sort descriptors into FlexQuery.NET sort nodes.
    /// </summary>
    /// <param name="sortModel">The collection of Kendo UI sort descriptors.</param>
    /// <returns>A list of FlexQuery.NET sort nodes.</returns>
    internal static List<SortNode> ParseSort(IReadOnlyList<KendoSortDescriptor>? sortModel)
    {
        return KendoSortParser.Parse(sortModel);
    }

    /// <summary>
    /// Processes group-level aggregates and adds them to the query options.
    /// </summary>
    /// <param name="groups">The group descriptors containing aggregates.</param>
    /// <param name="result">The QueryOptions to add aggregates to.</param>
    private static void ProcessGroupAggregates(IReadOnlyList<KendoGroupDescriptor> groups, QueryOptions result)
    {
        foreach (var group in groups)
        {
            if (group.Aggregates != null)
            {
                foreach (var aggregate in group.Aggregates)
                {
                    AddAggregate(result, aggregate.Field, aggregate.Aggregate);
                }
            }
        }
    }

    /// <summary>
    /// Adds an aggregate definition to the query options.
    /// </summary>
    /// <param name="result">The QueryOptions to add the aggregate to.</param>
    /// <param name="field">The field name to aggregate on.</param>
    /// <param name="aggregateFunction">The aggregate function name.</param>
    private static void AddAggregate(QueryOptions result, string? field, string? aggregateFunction)
    {
        if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(aggregateFunction)) return;

        var fn = NormalizeAggregateFunction(aggregateFunction);
        var alias = BuildAggregateAlias(fn, field);
        
        result.Aggregates.Add(new AggregateModel
        {
            Field = field,
            Function = fn,
            Alias = alias
        });
    }

    /// <summary>
    /// Deserializes a JsonElement into a KendoRequest object.
    /// </summary>
    /// <param name="root">The root JsonElement to deserialize.</param>
    /// <returns>A KendoRequest object.</returns>
    /// <exception cref="FormatException">Thrown when the JSON is not an object.</exception>
    private static KendoRequest DeserializeRequest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Kendo request JSON must be an object.");
        }

        var request = new KendoRequest();

        request.Page = GetInt(root, "page", 1);
        request.PageSize = GetInt(root, "pageSize", 0);
        request.Skip = GetInt(root, "skip", 0);
        request.Take = GetInt(root, "take", 0);

        if (root.TryGetProperty("filter", out var filterElement))
        {
            request.Filter = DeserializeFilter(filterElement);
        }

        if (root.TryGetProperty("sort", out var sortElement) && sortElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sortElement.EnumerateArray())
            {
                request.Sort.Add(DeserializeSortDescriptor(item));
            }
        }

        if (root.TryGetProperty("group", out var groupElement) && groupElement.ValueKind == JsonValueKind.Array)
        {
            request.Group = new List<KendoGroupDescriptor>();
            foreach (var item in groupElement.EnumerateArray())
            {
                request.Group.Add(DeserializeGroupDescriptor(item));
            }
        }

        if (root.TryGetProperty("aggregate", out var aggregatesElement) && aggregatesElement.ValueKind == JsonValueKind.Array)
        {
            request.Aggregates = new List<KendoAggregateDescriptor>();
            foreach (var item in aggregatesElement.EnumerateArray())
            {
                request.Aggregates.Add(new KendoAggregateDescriptor
                {
                    Field = GetString(item, "field"),
                    Aggregate = GetString(item, "aggregate")
                });
            }
        }

        return request;
    }

    /// <summary>
    /// Deserializes a JsonElement into a KendoFilter object.
    /// </summary>
    /// <param name="element">The JsonElement to deserialize.</param>
    /// <returns>A KendoFilter object.</returns>
    private static KendoFilter DeserializeFilter(JsonElement element)
    {
        var filter = new KendoFilter
        {
            Logic = GetString(element, "logic")
        };

        if (element.TryGetProperty("filters", out var filtersElement) && filtersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var filterItem in filtersElement.EnumerateArray())
            {
                filter.Filters.Add(DeserializeFilterDescriptor(filterItem));
            }
        }

        return filter;
    }

    /// <summary>
    /// Deserializes a JsonElement into a KendoFilterDescriptor object.
    /// </summary>
    /// <param name="element">The JsonElement to deserialize.</param>
    /// <returns>A KendoFilterDescriptor object.</returns>
    private static KendoFilterDescriptor DeserializeFilterDescriptor(JsonElement element)
    {
        var descriptor = new KendoFilterDescriptor
        {
            Field = GetString(element, "field"),
            Operator = GetString(element, "operator"),
            Logic = GetString(element, "logic")
        };

        if (element.TryGetProperty("value", out var valueElement))
        {
            descriptor.Value = valueElement.Clone();
        }

        if (element.TryGetProperty("filters", out var filtersElement) && filtersElement.ValueKind == JsonValueKind.Array)
        {
            descriptor.Filters = new List<KendoFilterDescriptor>();
            foreach (var filterItem in filtersElement.EnumerateArray())
            {
                descriptor.Filters.Add(DeserializeFilterDescriptor(filterItem));
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Deserializes a JsonElement into a KendoSortDescriptor object.
    /// </summary>
    /// <param name="element">The JsonElement to deserialize.</param>
    /// <returns>A KendoSortDescriptor object.</returns>
    private static KendoSortDescriptor DeserializeSortDescriptor(JsonElement element)
    {
        return new KendoSortDescriptor
        {
            Field = GetString(element, "field"),
            Dir = GetString(element, "dir")
        };
    }

    /// <summary>
    /// Deserializes a JsonElement into a KendoGroupDescriptor object.
    /// </summary>
    /// <param name="element">The JsonElement to deserialize.</param>
    /// <returns>A KendoGroupDescriptor object.</returns>
    private static KendoGroupDescriptor DeserializeGroupDescriptor(JsonElement element)
    {
        var descriptor = new KendoGroupDescriptor
        {
            Field = GetString(element, "field"),
            Dir = GetString(element, "dir")
        };

        if (element.TryGetProperty("aggregates", out var aggregatesElement) && aggregatesElement.ValueKind == JsonValueKind.Array)
        {
            descriptor.Aggregates = new List<KendoAggregateDescriptor>();
            foreach (var item in aggregatesElement.EnumerateArray())
            {
                descriptor.Aggregates.Add(new KendoAggregateDescriptor
                {
                    Field = GetString(item, "field"),
                    Aggregate = GetString(item, "aggregate")
                });
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Gets a string property value from a JsonElement.
    /// </summary>
    /// <param name="element">The JsonElement to read from.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The string value, or null if the property doesn't exist.</returns>
    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    /// <summary>
    /// Gets an integer property value from a JsonElement.
    /// </summary>
    /// <param name="element">The JsonElement to read from.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="defaultValue">The default value if the property doesn't exist or is invalid.</param>
    /// <returns>The integer value.</returns>
    private static int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? (property.TryGetInt32(out var val) ? val : defaultValue)
            : defaultValue;
    }

    /// <summary>
    /// Normalizes an aggregate function name to its canonical form.
    /// </summary>
    /// <param name="function">The aggregate function name to normalize.</param>
    /// <returns>The normalized function name.</returns>
    private static string NormalizeAggregateFunction(string function)
    {
        var normalized = function.Trim().ToLowerInvariant();
        return normalized switch
        {
            "average" or "avg" => "avg",
            "count" => "count",
            "sum" => "sum",
            "min" => "min",
            "max" => "max",
            _ => normalized
        };
    }

    /// <summary>
    /// Builds an alias for aggregate functions (e.g., "sum_total" or "count_All").
    /// </summary>
    /// <param name="function">The aggregate function name.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The generated alias.</returns>
    private static string BuildAggregateAlias(string function, string? field)
    {
        var normalized = string.IsNullOrWhiteSpace(field)
            ? "All"
            : field.Replace('.', '_');

        return $"{function.ToUpperInvariant()}_{normalized}";
    }
}
