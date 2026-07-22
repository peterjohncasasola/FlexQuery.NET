using System.Data;
using System.Data.Common;
using System.Reflection;
using Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class DapperRowHydrator
{
    #region Public entry points (joined-stream hydration)

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
        var result = HydrateCore<T>(rows, mapping, registry, paths);

        foreach (var node in filteredIncludes)
            ApplyExpandConfiguration(result, node, mapping, registry);

        return result;
    }

    #endregion

    #region Split-query include loading

    public static async Task<IReadOnlyList<T>> HydrateSplitQueryIncludesAsync<T>(
        IReadOnlyList<T> roots,
        IEntityMapping mapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        DbConnection connection,
        List<IncludeNode> expandNodes,
        SqlParameterContext sharedParameters,
        SqlTranslator sqlTranslator,
        CancellationToken cancellationToken)
        where T : class
    {
        if (roots.Count == 0) return roots;

        foreach (var node in expandNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadExpandNodeAsync(
                roots.Cast<object>().ToList(),
                mapping,
                registry,
                dialect,
                connection,
                node,
                sharedParameters,
                sqlTranslator,
                cancellationToken);
        }

        return roots;
    }

    private static async Task LoadExpandNodeAsync(
        IReadOnlyList<object> parents,
        IEntityMapping parentMapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        DbConnection connection,
        IncludeNode node,
        SqlParameterContext sharedParameters,
        SqlTranslator sqlTranslator,
        CancellationToken cancellationToken)
    {
        if (parents.Count == 0) return;

        var loadedChildren = await LoadNavigationAsync(
            parents,
            parentMapping,
            registry,
            dialect,
            connection,
            node.Path,
            sharedParameters,
            sqlTranslator,
            node,
            cancellationToken);

        if (loadedChildren.Count == 0 || node.Children.Count == 0)
            return;

        var rel = parentMapping.GetRelationship(node.Path);
        if (rel?.TargetType == null) return;

        var childMapping = registry.GetMapping(rel.TargetType);
        foreach (var childNode in node.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadExpandNodeAsync(
                loadedChildren,
                childMapping,
                registry,
                dialect,
                connection,
                childNode,
                sharedParameters,
                sqlTranslator,
                cancellationToken);
        }
    }

    internal static async Task<IReadOnlyList<object>> LoadNavigationAsync(
        IReadOnlyList<object> parents,
        IEntityMapping parentMapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        DbConnection connection,
        string navigationPath,
        SqlParameterContext parameters,
        SqlTranslator sqlTranslator,
        IncludeNode? expandNode,
        CancellationToken cancellationToken)
    {
        if (parents.Count == 0) return [];

        var sql = BuildIncludeSql(navigationPath, parentMapping, registry, dialect, sqlTranslator, expandNode, parents, parameters);
        if (string.IsNullOrEmpty(sql)) return [];

        var rows = await connection.QueryAsync(sql, parameters.RawParameters, commandType: CommandType.Text);
        var rowList = rows.ToList();
        if (!rowList.Any()) return [];

        var rel = parentMapping.GetRelationship(navigationPath);
        if (rel?.TargetType == null) return [];

        var targetMapping = registry.GetMapping(rel.TargetType);
        var navPrefix = navigationPath + "_";
        var hydrated = HydrateCoreNonGeneric(
            rowList, targetMapping, registry, new List<string> { string.Empty }, null, navPrefix).ToList();

        AttachChildrenToParents(parents, parentMapping, navigationPath, rel.ForeignKey, hydrated);
        return hydrated;
    }

    private static void AttachChildrenToParents(
        IReadOnlyList<object> parents,
        IEntityMapping parentMapping,
        string navigationPath,
        string foreignKey,
        IReadOnlyList<object> children)
    {
        var parentKeyProp = parentMapping.GetKeyProperties().FirstOrDefault()
            ?? parentMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? parentMapping.GetProperties().First();
        var parentPkPropInfo = parentMapping.Type.GetProperty(parentKeyProp, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (parentPkPropInfo == null) return;

        var parentIndex = parents
            .Select(parent => new { Key = parentPkPropInfo.GetValue(parent), Parent = parent })
            .Where(x => x.Key != null)
            .ToDictionary(x => x.Key!, x => x.Parent);

        foreach (var child in children)
        {
            var childType = child.GetType();
            var childFkProp = childType.GetProperty(foreignKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (childFkProp == null) continue;

            var childFk = childFkProp.GetValue(child);
            if (childFk == null) continue;

            if (parentIndex.TryGetValue(childFk, out var parent))
                AddChildToParent(parent, navigationPath, child);
        }
    }

    #endregion

    #region Core joined-stream hydration

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

    internal static IReadOnlyList<T> HydrateCore<T>(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        IReadOnlyList<string> includes,
        Dictionary<string, string>? columnAliasMap = null,
        string prefix = "")
        where T : class
    {
        return HydrateCoreNonGeneric(rows, mapping, registry, includes, columnAliasMap, prefix).Cast<T>().ToList();
    }

    internal static IEnumerable<object> HydrateCoreNonGeneric(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        IReadOnlyList<string> includes,
        Dictionary<string, string>? columnAliasMap = null,
        string prefix = "")
    {
        var method = typeof(DapperRowHydrator)
            .GetMethod(nameof(HydrateCoreGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

        var generic = method.MakeGenericMethod(mapping.Type);
        return (IEnumerable<object>)generic.Invoke(null, [rows, mapping, registry, includes, columnAliasMap, prefix])!;
    }

    private static IReadOnlyList<T> HydrateCoreGeneric<T>(
        IEnumerable<dynamic> rows,
        IEntityMapping mapping,
        IMappingRegistry registry,
        IReadOnlyList<string> includes,
        Dictionary<string, string>? columnAliasMap = null,
        string prefix = "")
        where T : class
    {
        var parentMap = new Dictionary<object, T>();
        var pkProperty = mapping.GetKeyProperties().FirstOrDefault()
            ?? mapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? mapping.GetProperties().First();
        var pkColumn = prefix + mapping.GetColumnName(pkProperty);

        var includeInfos = new List<(string ColumnKey, IEntityMapping TargetMapping, string NavProperty)>();
        foreach (var include in includes)
        {
            var rel = mapping.GetRelationship(include);
            if (rel?.TargetType is null) continue;

            var targetMapping = registry.GetMapping(rel.TargetType);
            var childPkProperty = targetMapping.GetKeyProperties().FirstOrDefault()
                ?? targetMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
                ?? targetMapping.GetProperties().First();
            var childPkColumn = prefix + include + "_" + targetMapping.GetColumnName(childPkProperty);

            includeInfos.Add((childPkColumn, targetMapping, include));
        }

        var rowKeysComparer = StringComparer.OrdinalIgnoreCase;

        foreach (var row in rows)
        {
            var rowDict = (IDictionary<string, object>)row;
            var rowKeys = rowDict.Keys.ToDictionary(k => k, k => k, rowKeysComparer);

            if (!rowKeys.TryGetValue(pkColumn, out var actualPkCol)
                || rowDict[actualPkCol] == DBNull.Value)
            {
                continue;
            }

            var pkValue = rowDict[actualPkCol];

            if (!parentMap.TryGetValue(pkValue, out var parent))
            {
                parent = MapRowToEntity<T>(rowDict, mapping, prefix, columnAliasMap);
                parentMap[pkValue] = parent;
            }

            foreach (var (childPkColumn, targetMapping, navProperty) in includeInfos)
            {
                if (rowKeys.TryGetValue(childPkColumn, out var actualChildPkCol)
                    && rowDict[actualChildPkCol] != DBNull.Value)
                {
                    var child = MapRowToEntity(rowDict, targetMapping, prefix + navProperty + "_", columnAliasMap);
                    AddChildToParent(parent, navProperty, child);
                }
            }
        }

        return parentMap.Values.ToList();
    }

    #endregion

    #region Entity mapping

    private static T MapRowToEntity<T>(IDictionary<string, object> row, IEntityMapping mapping, string prefix, Dictionary<string, string>? columnAliasMap = null) where T : class
        => (T)MapRowToEntity(row, mapping, prefix, columnAliasMap);

    private static object MapRowToEntity(IDictionary<string, object> row, IEntityMapping mapping, string prefix, Dictionary<string, string>? columnAliasMap = null)
    {
        var entity = Activator.CreateInstance(mapping.Type)!;
        var rowKeys = row.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var propName in mapping.GetProperties())
        {
            var colName = prefix + mapping.GetColumnName(propName);

            if (TryGetRowValue(row, rowKeys, colName, columnAliasMap, out var val) && val != DBNull.Value)
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
                    }
                }
            }
        }

        return entity;
    }

    private static bool TryGetRowValue(
        IDictionary<string, object> row,
        Dictionary<string, string> rowKeys,
        string expectedKey,
        Dictionary<string, string>? columnAliasMap,
        out object? value)
    {
        if (rowKeys.TryGetValue(expectedKey, out var actualKey) && row.TryGetValue(actualKey, out value))
            return true;

        if (columnAliasMap != null)
        {
            foreach (var kvp in columnAliasMap)
            {
                if (string.Equals(kvp.Value, expectedKey, StringComparison.OrdinalIgnoreCase)
                    && rowKeys.TryGetValue(kvp.Key, out actualKey)
                    && row.TryGetValue(actualKey, out value))
                    return true;
            }
        }

        value = null!;
        return false;
    }

    #endregion

    #region Child attachment

    internal static void AddChildToParent(object parent, string navigationProperty, object child)
    {
        var prop = parent.GetType().GetProperty(
            navigationProperty,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

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

    #endregion

    #region Split-query child attachment helpers

    private static bool TryGetChildForeignKey(object child, string foreignKeyPropertyName, out object? fkValue)
    {
        fkValue = null;
        var childType = child.GetType();
        var prop = childType.GetProperty(foreignKeyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null) return false;

        fkValue = prop.GetValue(child);
        return fkValue != null;
    }

    private static bool TryGetRootPrimaryKey(object root, IEntityMapping rootMapping, out object? pkValue)
    {
        pkValue = null;
        var keyProp = rootMapping.GetKeyProperties().FirstOrDefault()
            ?? rootMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? rootMapping.GetProperties().First();

        var prop = root.GetType().GetProperty(keyProp, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null) return false;

        pkValue = prop.GetValue(root);
        return pkValue != null;
    }

    #endregion

    #region Expand post-hydration (in-memory sort/take fallback)

    internal static void ApplyExpandConfiguration<T>(
        IReadOnlyList<T> entities,
        IncludeNode node,
        IEntityMapping mapping,
        IMappingRegistry registry)
        where T : class
    {
        if (entities.Count == 0) return;

        var rel = mapping.GetRelationship(node.Path);
        if (rel?.TargetType == null) return;

        var targetMapping = registry.GetMapping(rel.TargetType);

        foreach (var entity in entities)
        {
            var collectionProp = entity.GetType().GetProperty(
                node.Path,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (collectionProp == null) continue;

            var collection = collectionProp.GetValue(entity) as System.Collections.IEnumerable;
            if (collection == null) continue;

            var items = collection.Cast<object>().ToList();

            if (node.Sort is { Count: > 0 })
                items = SortItems(items, node.Sort, targetMapping).ToList();

            if (node.Take.HasValue)
                items = items.Take(node.Take.Value).ToList();

            var listType = typeof(List<>).MakeGenericType(rel.TargetType);
            var newCollection = Activator.CreateInstance(listType);
            foreach (var item in items)
                listType.GetMethod("Add")!.Invoke(newCollection, [item]);

            collectionProp.SetValue(entity, newCollection);
        }

        foreach (var childNode in node.Children)
        {
            foreach (var entity in entities)
            {
                var collectionProp = entity.GetType().GetProperty(
                    node.Path,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (collectionProp == null) continue;

                var collection = collectionProp.GetValue(entity) as System.Collections.IEnumerable;
                if (collection == null) continue;

                var childEntities = collection.Cast<object>().ToList();
                ApplyExpandConfiguration(childEntities, childNode, targetMapping, registry);
            }
        }
    }

    private static IEnumerable<object> SortItems(
        IEnumerable<object> items,
        List<SortNode> sortNodes,
        IEntityMapping mapping)
    {
        var sortNode = sortNodes[0];
        var prop = mapping.Type.GetProperty(
            sortNode.Field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (prop == null) return items;

        return sortNode.Descending
            ? items.OrderByDescending(item => GetPropertyValue(item, prop))
            : items.OrderBy(item => GetPropertyValue(item, prop));
    }

    private static object? GetPropertyValue(object obj, PropertyInfo prop)
    {
        var value = prop.GetValue(obj);
        return value is IComparable ? value : null;
    }

    #endregion

    #region Include SQL builder helper

    private static string BuildIncludeSql(
        string navigationPath,
        IEntityMapping rootMapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        SqlTranslator sqlTranslator,
        IncludeNode? expandNode,
        IReadOnlyList<object> roots,
        SqlParameterContext parameters)
    {
        var rel = rootMapping.GetRelationship(navigationPath);
        if (rel?.TargetType == null) return string.Empty;

        var targetMapping = registry.GetMapping(rel.TargetType);

        var pkProperty = rootMapping.GetKeyProperties().FirstOrDefault()
            ?? rootMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? rootMapping.GetProperties().First();

        var rootPks = new List<object?>();
        foreach (var root in roots)
        {
            var pkVal = root.GetType().GetProperty(pkProperty)?.GetValue(root);
            if (pkVal != null) rootPks.Add(pkVal);
        }

        if (rootPks.Count == 0) return string.Empty;

        if (expandNode != null)
        {
            return sqlTranslator.BuildIncludeSql(
                navigationPath,
                rootMapping,
                targetMapping,
                parameters,
                rootPks,
                expandNode);
        }

        return sqlTranslator.BuildIncludeSql(
            navigationPath,
            rootMapping,
            targetMapping,
            parameters,
            rootPks);
    }

    #endregion
}
