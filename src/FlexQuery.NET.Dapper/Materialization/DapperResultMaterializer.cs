using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Materialization;

/// <summary>
/// Converts raw Dapper query results into the final object shape expected by
/// FlexQuery, including entity hydration, grouped projections, and plain
/// dictionary results.
/// </summary>
internal static class DapperResultMaterializer
{
    public static IReadOnlyList<object> Materialize(
        IEnumerable<dynamic> rows,
        QueryOptions queryOptions,
        Func<IEnumerable<dynamic>, IReadOnlyList<object>> hydrateIncludes,
        CancellationToken cancellationToken)
    {
        if (queryOptions.Includes?.Count > 0 ||
            queryOptions.Expand?.Count > 0)
        {
            return hydrateIncludes(rows);
        }

        var isGroupedOrAggregated =
            queryOptions.GroupBy?.Count > 0 ||
            queryOptions.Aggregates.Count > 0;

        return isGroupedOrAggregated ? MaterializeGroupedRows(rows, cancellationToken) : ToPlainDictionaries(rows);
    }

    private static IReadOnlyList<object> ToPlainDictionaries(
        IEnumerable<dynamic> rows)
    {
        return rows
            .Select(row => (object)new Dictionary<string, object>(
                (IDictionary<string, object>)row,
                StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<object> MaterializeGroupedRows(
        IEnumerable<dynamic> rows,
        CancellationToken cancellationToken)
    {
        var dictionaries = rows
            .Select(row => (IDictionary<string, object>)row)
            .ToList();

        if (dictionaries.Count == 0)
            return [];

        var propertyTypes = dictionaries[0]
            .Keys
            .ToDictionary(
                key => key,
                _ => typeof(object),
                StringComparer.OrdinalIgnoreCase);

        var projectedType =
            DynamicTypeBuilder.GetDynamicType(propertyTypes);

        cancellationToken.ThrowIfCancellationRequested();

        return dictionaries
            .Select(row =>
            {
                var instance = Activator.CreateInstance(projectedType)!;

                foreach (var kvp in row)
                {
                    var property = projectedType.GetProperty(kvp.Key);

                    if (property is { CanWrite: true })
                    {
                        property.SetValue(instance, kvp.Value);
                    }
                }

                return instance;
            })
            .ToList();
    }
}