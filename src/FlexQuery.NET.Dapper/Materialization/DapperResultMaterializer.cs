using System.Collections;
using System.Dynamic;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Metadata;

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
        CancellationToken cancellationToken,
        Func<string, string>? propertyNameTransformer = null)
    {
        if (queryOptions.Includes?.Count > 0 ||
            queryOptions.Expand?.Count > 0)
        {
            return hydrateIncludes(rows);
        }

        var isGroupedOrAggregated =
            queryOptions.GroupBy?.Count > 0 ||
            queryOptions.Aggregates.Count > 0;

        return isGroupedOrAggregated ? MaterializeGroupedRows(rows, cancellationToken) : ToPlainDictionaries(rows, propertyNameTransformer);
    }

    private static IReadOnlyList<object> ToPlainDictionaries(
        IEnumerable<dynamic> rows,
        Func<string, string>? propertyNameTransformer)
    {
        return rows
            .Select(row =>
            {
                var source = (IDictionary<string, object>)row;
                if (propertyNameTransformer is null)
                {
                    return (object)new Dictionary<string, object>(source, StringComparer.OrdinalIgnoreCase);
                }

                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in source)
                {
                    dict[propertyNameTransformer(kvp.Key)] = kvp.Value;
                }

                return dict;
            })
            .ToList();
    }

    public static object ProjectEntity(object? entity, SelectionNode selectTree, Func<string, string>? propertyNameTransformer = null)
    {
        var result = new ExpandoObject();
        IDictionary<string, object?> resultDict = result;

        if (selectTree.IncludeAllScalars)
        {
            foreach (var prop in entity?.GetType().GetProperties(
                         BindingFlags.Public | BindingFlags.Instance)!)
            {
                if (!prop.CanRead) continue;
                if (!TypeClassification.IsScalarType(prop.PropertyType))
                    continue;

                var value = prop.GetValue(entity);
                var outputName = propertyNameTransformer is null ? prop.Name : propertyNameTransformer(prop.Name);
                resultDict[outputName] = value!;
            }
        }

        foreach (var (propName, childNode) in selectTree.EnumerateChildren())
        {
            var prop = entity?.GetType().GetProperty(
                propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null) continue;

            var rawName = !string.IsNullOrWhiteSpace(childNode.Alias) ? childNode.Alias : prop.Name;
            var outputName = propertyNameTransformer is null ? rawName : propertyNameTransformer(rawName);
            var value = prop.GetValue(entity);

            if (childNode.HasChildren || childNode.IncludeAllScalars)
            {
                if (value is IEnumerable enumerable and not string)
                {
                    var items = (from object? item in enumerable select ProjectEntity(item, childNode, propertyNameTransformer)).ToList();
                    resultDict[outputName] = items;
                }
                else if (value != null)
                {
                    resultDict[outputName] = ProjectEntity(value, childNode, propertyNameTransformer);
                }
            }
            else
            {
                resultDict[outputName] = value;
            }
        }

        return result;
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
