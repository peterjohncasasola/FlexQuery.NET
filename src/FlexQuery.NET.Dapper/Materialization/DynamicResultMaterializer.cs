using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class DynamicResultMaterializer
{
    public static IReadOnlyList<object> HandleDynamicOrGroupingResults(
        IEnumerable<dynamic> dynamicItems, QueryOptions options, CancellationToken ct)
    {
        var useGrouping = options.GroupBy?.Count > 0 || options.Aggregates.Count > 0;

        var rows = dynamicItems
            .Select(d => (IDictionary<string, object>)d)
            .ToList();

        if (rows.Count == 0)
            return Array.Empty<object>();

        if (!useGrouping)
        {
            // Return each row as a plain dictionary
            return rows
                .Select(r => (object)new Dictionary<string, object>(r, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        // For grouping/aggregate results, build a dynamic type with all the returned columns
        var colTypes = rows[0].Keys
            .ToDictionary(k => k, _ => typeof(object), StringComparer.OrdinalIgnoreCase);

        var projectedType = DynamicTypeBuilder.GetDynamicType(
            new Dictionary<string, Type>(colTypes));

        ct.ThrowIfCancellationRequested();

        return rows.Select(row =>
        {
            var instance = Activator.CreateInstance(projectedType)!;
            foreach (var kvp in row)
            {
                var prop = projectedType.GetProperty(kvp.Key);
                if (prop is { CanWrite: true })
                    prop.SetValue(instance, kvp.Value);
            }
            return instance;
        }).ToList();
    }
}