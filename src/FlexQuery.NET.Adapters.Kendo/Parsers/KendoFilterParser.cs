using System.Text.Json;
using FlexQuery.NET.Adapters.Kendo.Mapping;
using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Adapters.Kendo.Parsers;

/// <summary>
/// Parses Kendo UI filter structures into FlexQuery.NET filter groups.
/// </summary>
internal static class KendoFilterParser
{
    /// <summary>
    /// Parses a Kendo UI filter into a FlexQuery.NET filter group.
    /// </summary>
    /// <param name="filter">The Kendo UI filter to parse.</param>
    /// <returns>A FlexQuery.NET filter group, or null if the filter is empty.</returns>
    public static FilterGroup? Parse(KendoFilter? filter)
    {
        if (filter == null || filter.Filters.Count == 0)
        {
            return null;
        }

        var group = new FilterGroup
        {
            Logic = ParseLogic(filter.Logic)
        };

        foreach (var filterDescriptor in filter.Filters)
        {
            var parsedFilter = ParseFilterDescriptor(filterDescriptor);
            if (parsedFilter != null)
            {
                MergeInto(group, parsedFilter);
            }
        }

        return group;
    }

    /// <summary>
    /// Parses a single Kendo UI filter descriptor into a FlexQuery.NET filter group.
    /// </summary>
    /// <param name="descriptor">The filter descriptor to parse.</param>
    /// <returns>A FlexQuery.NET filter group, or null if the descriptor is invalid.</returns>
    private static FilterGroup? ParseFilterDescriptor(KendoFilterDescriptor descriptor)
    {
        // If this descriptor has nested filters, it's a logical group
        if (descriptor.Filters != null && descriptor.Filters.Count > 0)
        {
            var group = new FilterGroup
            {
                Logic = ParseLogic(descriptor.Logic)
            };

            foreach (var nestedFilter in descriptor.Filters)
            {
                var parsedFilter = ParseFilterDescriptor(nestedFilter);
                if (parsedFilter != null)
                {
                    MergeInto(group, parsedFilter);
                }
            }

            return group;
        }

        // Otherwise, it's a simple filter condition
        if (string.IsNullOrWhiteSpace(descriptor.Field) || string.IsNullOrWhiteSpace(descriptor.Operator))
        {
            return null;
        }

        var flexOperator = KendoOperatorMapper.Map(descriptor.Operator);
        var value = FormatValue(descriptor.Value, flexOperator, descriptor.Field);

        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            {
                new FilterCondition
                {
                    Field = descriptor.Field,
                    Operator = flexOperator,
                    Value = value
                }
            }
        };
    }

    /// <summary>
    /// Parses a Kendo UI logic operator string into a FlexQuery.NET logic operator.
    /// </summary>
    /// <param name="logic">The logic string ("and" or "or").</param>
    /// <returns>The corresponding FlexQuery.NET logic operator.</returns>
    private static LogicOperator ParseLogic(string? logic)
    {
        return ParserUtilities.ParseLogic(logic);
    }

    /// <summary>
    /// Formats a JsonElement value into a string representation for FlexQuery.NET.
    /// </summary>
    /// <param name="value">The JsonElement to format.</param>
    /// <param name="flexOperator">The FlexQuery.NET operator.</param>
    /// <param name="fieldPath">The field path for error messages.</param>
    /// <returns>The formatted string value, or null for null-check operators.</returns>
    private static string? FormatValue(JsonElement value, string flexOperator, string fieldPath)
    {
        if (flexOperator is FilterOperators.IsNull or FilterOperators.IsNotNull)
        {
            return null;
        }

        return Format(value);
    }

    /// <summary>
    /// Converts a JsonElement to its string representation.
    /// </summary>
    /// <param name="element">The JsonElement to convert.</param>
    /// <returns>The string representation of the element.</returns>
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

    /// <summary>
    /// Merges a source filter group into a target filter group, optimizing for flat structures.
    /// </summary>
    /// <param name="target">The target filter group to merge into.</param>
    /// <param name="source">The source filter group to merge from.</param>
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
