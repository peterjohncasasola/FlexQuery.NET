using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Adapters.AgGrid.Converters;

/// <summary>
/// Converts FlexQuery query results into AG Grid SSRM-compatible response payloads.
/// </summary>
/// <remarks>
/// When the request targets a grouping level, <see cref="QueryResult{T}.Data"/> must already
/// contain one row per group for that level, produced by FlexQuery using the parsed
/// <see cref="QueryOptions.GroupBy"/> and aggregate definitions. This converter does not perform
/// grouping, distinct processing, or aggregation. It only reshapes the supplied result rows into
/// an AG Grid Server-Side Row Model response.
/// </remarks>
internal static class AgGridResponseConverter
{
    private sealed record PropertyEntry(PropertyInfo Info, string Name, string CamelName);

    private static readonly ConcurrentDictionary<Type, PropertyEntry[]> ReadablePropertyCache = new();

    /// <summary>
    /// Converts a FlexQuery result for the current SSRM request into group rows or leaf rows.
    /// </summary>
    /// <typeparam name="T">The result row type.</typeparam>
    /// <param name="request">The AG Grid SSRM request that produced the query.</param>
    /// <param name="result">
    /// The FlexQuery result. Grouped data must already be grouped and aggregated by FlexQuery at
    /// the grouping level selected by the request.
    /// </param>
    /// <param name="camelCase">
    /// When <c>true</c>, converts POCO property names to camelCase in the row data dictionaries.
    /// When <c>false</c> (default), property names are preserved as-is from the CLR type.
    /// </param>
    /// <param name="options">Optional names for adapter-defined response metadata fields.</param>
    /// <returns>An AG Grid SSRM response for the current store level.</returns>
    internal static AgGridServerSideResponse Convert<T>(
        AgGridRequest request,
        QueryResult<T> result,
        bool camelCase = false,
        AgGridResponseFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        options ??= new AgGridResponseFieldOptions();

        var rowGroupFields = request.RowGroupCols?
            .Select(c => c.Field?.Trim())
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Cast<string>()
            .ToList() ?? [];

        var level = request.GroupKeys.Count;
        var rows = result.Data
            .Select(row => ToDictionary(row, camelCase))
            .Select(row => MapAggregateAliases(row, request.ValueCols))
            .ToList();

        if (rowGroupFields.Count == 0 || level >= rowGroupFields.Count)
        {
            return new AgGridServerSideResponse
            {
                RowData = rows,
                RowCount = ResolveRowCount(result)
            };
        }

        var currentField = rowGroupFields[level];
        var leafGroup = level == rowGroupFields.Count - 1;

        var groupRows = rows
            .Select(row => ToGroupRow(row, currentField, level, leafGroup, request.GroupKeys, options))
            .Select(group => ToDictionary(group, camelCase, options))
            .ToList();

        return new AgGridServerSideResponse
        {
            RowData = groupRows,
            RowCount = ResolveRowCount(result)
        };
    }

    private static int? ResolveRowCount<T>(QueryResult<T> result)
    {
        return result.ResultCount ?? result.TotalCount;
    }

    private static AgGridGroupRow ToGroupRow(
        IReadOnlyDictionary<string, object?> row,
        string currentField,
        int level,
        bool leafGroup,
        IReadOnlyList<string> groupKeys,
        AgGridResponseFieldOptions options)
    {
        row.TryGetValue(currentField, out var keyValue);
        var key = keyValue?.ToString() ?? string.Empty;
        var childCount = ResolveChildCount(row, options);
        var route = groupKeys.Concat([key]).ToList();

        return new AgGridGroupRow
        {
            Key = key,
            Field = currentField,
            Level = level,
            LeafGroup = leafGroup,
            ChildCount = childCount,
            Route = route,
            Values = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static Dictionary<string, object?> ToDictionary(AgGridGroupRow row, bool camelCase, AgGridResponseFieldOptions options)
    {
        var result = camelCase
            ? new Dictionary<string, object?>(row.Values.Count + 5, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase);

        if (camelCase)
        {
            foreach (var kvp in row.Values)
            {
                result[ToCamelCase(kvp.Key)] = kvp.Value;
            }
        }

        result[options.GroupFlagFieldName] = row.Group;
        result[options.KeyFieldName] = row.Key;
        result[options.FieldFieldName] = row.Field;
        result[options.LevelFieldName] = row.Level;
        result[options.LeafGroupFieldName] = row.LeafGroup;
        result[options.RouteFieldName] = row.Route;

        if (row.ChildCount.HasValue)
        {
            result[options.ChildCountFieldName] = row.ChildCount.Value;
        }

        return result;
    }

    private static Dictionary<string, object?> ToDictionary<T>(T row, bool camelCase)
    {
        if (row is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (row is IReadOnlyDictionary<string, object?> readOnlyDict)
        {
            return new Dictionary<string, object?>(readOnlyDict, StringComparer.OrdinalIgnoreCase);
        }

        if (row is IDictionary<string, object?> dict)
        {
            return new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
        }

        if (row is IDictionary nonGenericDictionary)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in nonGenericDictionary)
            {
                if (item.Key is not null)
                {
                    values[item.Key.ToString()!] = item.Value;
                }
            }

            return values;
        }

        var entries = ReadablePropertyCache.GetOrAdd(row.GetType(), static type =>
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();
            return props.Select(p => new PropertyEntry(p, p.Name, ToCamelCase(p.Name))).ToArray();
        });

        var result = new Dictionary<string, object?>(entries.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            result[camelCase ? entry.CamelName : entry.Name] = entry.Info.GetValue(row);
        }

        return result;
    }

    private static Dictionary<string, object?> MapAggregateAliases(
        Dictionary<string, object?> row,
        IReadOnlyList<AgGridValueColumn>? valueCols)
    {
        if (valueCols is null || valueCols.Count == 0)
            return row;

        var fieldGroups = valueCols
            .Where(v => !string.IsNullOrWhiteSpace(v.Field) && !string.IsNullOrWhiteSpace(v.AggFunc))
            .GroupBy(v => v.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fieldGroups.Count == 0)
            return row;

        var aliasToField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in fieldGroups)
        {
            if (group.Count() != 1) continue;

            var col = group.Single();
            var fn = NormalizeAggFunc(col.AggFunc!);
            var alias = ParserUtilities.BuildAggregateAlias(fn, col.Field);

            if (row.ContainsKey(alias) && !row.ContainsKey(col.Field!))
            {
                aliasToField[alias] = col.Field!;
            }
        }

        if (aliasToField.Count == 0)
            return row;

        var mapped = new Dictionary<string, object?>(row.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in row)
        {
            if (aliasToField.TryGetValue(kvp.Key, out var field))
            {
                mapped[field] = kvp.Value;
            }
            else
            {
                mapped[kvp.Key] = kvp.Value;
            }
        }

        return mapped;
    }

    private static string NormalizeAggFunc(string aggFunc)
    {
        var fn = aggFunc.ToLowerInvariant();
        return fn == "average" ? "avg" : fn;
    }

    private static int? ResolveChildCount(
        IReadOnlyDictionary<string, object?> row,
        AgGridResponseFieldOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ChildCountSourceField)
            && row.TryGetValue(options.ChildCountSourceField, out var sourceValue)
            && TryConvertInt(sourceValue, out var sourceCount))
        {
            return sourceCount;
        }

        if (row.TryGetValue(options.ChildCountFieldName, out var value) && TryConvertInt(value, out var count))
        {
            return count;
        }

        return null;
    }

    private static bool TryConvertInt(object? value, out int result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                result = (int)longValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            case string stringValue when int.TryParse(stringValue, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static string ToCamelCase(string name)
    {
        if (name.Length == 0 || char.IsLower(name[0]))
            return name;
        return string.Create(name.Length, name, (span, s) =>
        {
            span[0] = char.ToLowerInvariant(s[0]);
            s.AsSpan(1).CopyTo(span[1..]);
        });
    }
}
