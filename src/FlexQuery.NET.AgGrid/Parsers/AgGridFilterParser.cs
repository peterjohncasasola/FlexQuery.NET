using System.Text.Json;
using FlexQuery.NET.AgGrid.Mapping;
using FlexQuery.NET.AgGrid.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.AgGrid.Parsers;

public static class AgGridFilterParser
{
    public static FilterGroup Parse(IReadOnlyDictionary<string, AgGridFilterNode> filterModel)
    {
        ArgumentNullException.ThrowIfNull(filterModel);

        var group = new FilterGroup { Logic = LogicOperator.And };

        foreach (var filter in filterModel)
        {
            MergeInto(group, ParseField(filter.Key, filter.Value));
        }

        return group;
    }

    internal static FilterGroup ParseField(string fieldPath, AgGridFilterNode filter, string? defaultFilterType = null)
    {
        if (filter.Conditions.Count > 0)
        {
            return ParseMultiCondition(fieldPath, filter);
        }

        var filterType = (filter.FilterType ?? defaultFilterType ?? string.Empty).Trim().ToLowerInvariant();

        return filterType switch
        {
            "text" => ParseSingleCondition(fieldPath, filter, filterType),
            "number" => ParseSingleCondition(fieldPath, filter, filterType),
            "date" => ParseSingleCondition(fieldPath, filter, filterType),
            "set" => ParseSetFilter(fieldPath, filter),
            _ => throw new FormatException($"Unsupported AG Grid filter type '{filter.FilterType}' for field '{fieldPath}'.")
        };
    }

    private static FilterGroup ParseSingleCondition(string fieldPath, AgGridFilterNode filter, string filterType)
    {
        var flexOperator = AgGridOperatorMapper.Map(filterType, filter.Type);
        var value = ResolveValue(filterType, flexOperator, filter, fieldPath);

        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            {
                new FilterCondition
                {
                    Field = fieldPath,
                    Operator = flexOperator,
                    Value = value
                }
            }
        };
    }

    private static FilterGroup ParseSetFilter(string fieldPath, AgGridFilterNode filter)
    {
        var values = filter.Values
            .Select(Format)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();

        if (values.Count == 0)
        {
            return new FilterGroup { Logic = LogicOperator.And };
        }

        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            {
                new FilterCondition
                {
                    Field = fieldPath,
                    Operator = FilterOperators.In,
                    Value = string.Join(",", values)
                }
            }
        };
    }

    private static FilterGroup ParseMultiCondition(string fieldPath, AgGridFilterNode filter)
    {
        var logic = ParseLogic(filter.Operator, fieldPath);
        var group = new FilterGroup { Logic = logic };

        foreach (var condition in filter.Conditions)
        {
            MergeInto(group, ParseField(fieldPath, condition, filter.FilterType));
        }

        return group;
    }

    private static LogicOperator ParseLogic(string? operatorName, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return LogicOperator.And;
        }

        return operatorName.Trim().ToLowerInvariant() switch
        {
            "and" => LogicOperator.And,
            "or" => LogicOperator.Or,
            _ => throw new FormatException($"Unsupported AG Grid condition operator '{operatorName}' for field '{fieldPath}'.")
        };
    }

    private static string? ResolveValue(string filterType, string flexOperator, AgGridFilterNode filter, string fieldPath)
    {
        return flexOperator switch
        {
            FilterOperators.Between => filterType == "date"
                ? FormatRange(filter.DateFrom, filter.DateTo, fieldPath)
                : FormatRange(filter.Filter, filter.FilterTo, fieldPath),
            FilterOperators.IsNull or FilterOperators.IsNotNull => null,
            _ => filterType == "date"
                ? Format(GetFirstValue(filter, "dateFrom", "filter"))
                : Format(GetFirstValue(filter, "filter", "dateFrom"))
        };
    }

    private static string FormatRange(JsonElement? start, JsonElement? end, string fieldPath)
    {
        var startValue = Format(start);
        var endValue = Format(end);

        if (string.IsNullOrEmpty(startValue) || string.IsNullOrEmpty(endValue))
        {
            throw new FormatException($"AG Grid range filter '{fieldPath}' requires both start and end values.");
        }

        return $"{startValue},{endValue}";
    }

    private static JsonElement? GetFirstValue(AgGridFilterNode filter, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetElement(filter, propertyName);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static JsonElement? GetElement(AgGridFilterNode filter, string propertyName)
    {
        return propertyName.ToLowerInvariant() switch
        {
            "filter" => filter.Filter,
            "filterto" => filter.FilterTo,
            "datefrom" => filter.DateFrom,
            "dateto" => filter.DateTo,
            _ => null
        };
    }

    private static string? Format(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return null;
        }

        return Format(element.Value);
    }

    private static string Format(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static void MergeInto(FilterGroup target, FilterGroup source)
    {
        if (source.Filters.Count == 0 && source.Groups.Count == 0)
        {
            return;
        }

        if (!source.IsNegated && source.Groups.Count == 0 && source.Logic == LogicOperator.And)
        {
            target.Filters.AddRange(source.Filters);
            return;
        }

        target.Groups.Add(source);
    }
}
