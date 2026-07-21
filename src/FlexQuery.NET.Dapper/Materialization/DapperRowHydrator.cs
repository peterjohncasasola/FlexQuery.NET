using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class DapperRowHydrator
{
    public static IReadOnlyList<T> HydrateIncludes<T>(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        IReadOnlyList<string>? includes)
        where T : class
    {
        if (includes == null || includes.Count == 0)
            return rows.Cast<T>().ToList();

        return HydrateCore<T>(rows, mapping, registry, includes);
    }

    public static IReadOnlyList<T> HydrateFilteredIncludes<T>(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        List<IncludeNode>? filteredIncludes)
        where T : class
    {
        if (filteredIncludes == null || filteredIncludes.Count == 0)
            return rows.Cast<T>().ToList();

        var paths = ExtractIncludePaths(filteredIncludes);
        return HydrateCore<T>(rows, mapping, registry, paths);
    }

    private static List<string> ExtractIncludePaths(List<IncludeNode> nodes)
    {
        var paths = new List<string>();
        foreach (var node in nodes)
        {
            paths.Add(node.Path);
            if (node.Children.Count > 0)
                paths.AddRange(ExtractIncludePaths(node.Children));
        }
        return paths;
    }

    private static IReadOnlyList<T> HydrateCore<T>(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        IReadOnlyList<string> includes)
        where T : class
    {
        var parentMap = new Dictionary<object, T>();
        var pkProperty = mapping.GetKeyProperties().FirstOrDefault()
            ?? mapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? mapping.GetProperties().First();
        var pkColumn = mapping.GetColumnName(pkProperty);

        foreach (var row in rows)
        {
            var rowDict = (IDictionary<string, object>)row;
            var rowKeys = rowDict.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);

            if (!rowKeys.TryGetValue(pkColumn, out var actualPkCol)
                || rowDict[actualPkCol] == DBNull.Value)
            {
                continue;
            }

            var pkValue = rowDict[actualPkCol];

            if (!parentMap.TryGetValue(pkValue, out var parent))
            {
                parent = MapRowToEntity<T>(rowDict, mapping, string.Empty);
                parentMap[pkValue] = parent;
            }

            foreach (var include in includes)
            {
                var rel = mapping.GetRelationship(include);
                if (rel == null) continue;

                if (rel.TargetType is null) continue;

                var targetMapping = registry.GetMapping(rel.TargetType);
                var childPkProperty = targetMapping.GetKeyProperties().FirstOrDefault()
                    ?? targetMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    ?? targetMapping.GetProperties().First();
                var childPkColumn = include + "_" + targetMapping.GetColumnName(childPkProperty);

                if (rowKeys.TryGetValue(childPkColumn, out var actualChildPkCol)
                    && rowDict[actualChildPkCol] != DBNull.Value)
                {
                    var child = MapRowToEntity(rowDict, targetMapping, include + "_");
                    AddChildToParent(parent, include, child);
                }
            }
        }

        return parentMap.Values.ToList();
    }

    private static T MapRowToEntity<T>(IDictionary<string, object> row, IEntityMapping mapping, string prefix) where T : class
        => (T)MapRowToEntity(row, mapping, prefix);

    private static object MapRowToEntity(IDictionary<string, object> row, IEntityMapping mapping, string prefix)
    {
        var entity = Activator.CreateInstance(mapping.Type)!;
        var rowKeys = row.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var propName in mapping.GetProperties())
        {
            var colName = prefix + mapping.GetColumnName(propName);
            if (rowKeys.TryGetValue(colName, out var actualKey)
                && row.TryGetValue(actualKey, out var val)
                && val != DBNull.Value)
            {
                var prop = mapping.Type.GetProperty(propName);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        prop.SetValue(entity, Convert.ChangeType(val, targetType));
                    }
                    catch
                    {
                        // Preserve existing behavior: skip incompatible values during hydration.
                    }
                }
            }
        }

        return entity;
    }

    private static void AddChildToParent(object parent, string navigationProperty, object child)
    {
        var prop = parent.GetType().GetProperty(
            navigationProperty,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

        if (prop == null) return;

        var value = prop.GetValue(parent);
        if (value == null)
        {
            var propType = prop.PropertyType;
            if (propType.IsGenericType
                && (propType.GetGenericTypeDefinition() == typeof(List<>)
                    || propType.GetGenericTypeDefinition() == typeof(ICollection<>)
                    || propType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var itemType = propType.GetGenericArguments()[0];
                value = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                prop.SetValue(parent, value);
            }
            else
            {
                prop.SetValue(parent, child);
                return;
            }
        }

        if (value is System.Collections.IList list)
        {
            var childType = child.GetType();
            var childKeyProp = childType.GetProperties()
                .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));

            if (childKeyProp == null)
            {
                var keyProps = childType.GetProperties()
                    .Where(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                childKeyProp = keyProps.FirstOrDefault();
            }

            var childPk = childKeyProp?.GetValue(child);

            if (childPk != null)
            {
                foreach (var item in list)
                {
                    var itemPk = item.GetType().GetProperty(childKeyProp!.Name)?.GetValue(item);
                    if (childPk.Equals(itemPk))
                    {
                        return;
                    }
                }
            }

            list.Add(child);
        }
    }
}
