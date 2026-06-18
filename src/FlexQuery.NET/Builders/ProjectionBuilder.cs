using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Recursively constructs MemberInitExpressions for dynamic projection 
/// mapped to strongly-typed runtime classes, allowing full EF Core server-side translation.
/// Alias support: leaf nodes with an Alias emit that alias as the output property name.
/// </summary>
internal static class ProjectionBuilder
{
    private static readonly MethodInfo _queryableAsQueryable1 =
        typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.AsQueryable)
                        && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 1);

    private static readonly MethodInfo _queryableSelect2 =
        typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Select)
                        && m.GetParameters().Length == 2);

    private static readonly MethodInfo _enumerableToList1 =
        typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.ToList)
                        && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 1);

    public static Expression<Func<T, object>> Build<T>(SelectionNode selectTree, QueryOptions options)
    {
        if (Caching.QueryCacheManager.ShouldCache(options.EnableCache))
        {
            var cacheKey = options.GetCacheKey(typeof(T), "projection") + ":" + GenerateCacheKey(selectTree);
            return Caching.QueryCacheManager.GetOrAddExpression(cacheKey, () =>
            {
                var param = Expression.Parameter(typeof(T), "x");
                var memberInit = BuildMemberInit(param, typeof(T), selectTree, options.Filter, options);
                var boxed = Expression.Convert(memberInit, typeof(object));
                return Expression.Lambda<Func<T, object>>(boxed, param);
            });
        }

        var param = Expression.Parameter(typeof(T), "x");
        var memberInit = BuildMemberInit(param, typeof(T), selectTree, options.Filter, options);
        var boxed = Expression.Convert(memberInit, typeof(object));
        return Expression.Lambda<Func<T, object>>(boxed, param);
    }

    private static Expression BuildMemberInit(
        Expression source,
        Type sourceType,
        SelectionNode selectTree,
        FilterGroup? filterContext,
        QueryOptions options)
    {
        var effectiveNode = NormalizeSelection(sourceType, selectTree);

        // Key = output name (alias or prop name), Value = (clrType, expression)
        var propertiesToSelect = new Dictionary<string, (Type TargetType, Expression Assignment)>();

        foreach (var kvp in effectiveNode.EnumerateChildren())
        {
            var propName = kvp.Key;
            var childNode = kvp.Value;
            var outputName = !string.IsNullOrWhiteSpace(childNode.Alias) ? childNode.Alias : propName;

            Expression propAccess;
            Type propType;

            if (FieldResolver.TryResolveMappedExpression(source, propName, options, out var resolvedExpr, out var resolvedType))
            {
                propAccess = resolvedExpr;
                propType = resolvedType;
            }
            else
            {
                var propInfo = sourceType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propInfo == null) continue;
                propAccess = Expression.Property(source, propInfo);
                propType = propInfo.PropertyType;
            }

            if (ShouldBuildNestedProjection(propType, childNode))
            {
                var childFilterContext = MergeFilters(
                    filterContext != null ? FilterAnalyzer.ExtractForNavigation(filterContext!, propName) : null,
                    childNode.Filter);

                if (IsIEnumerable(propType, out var itemType))
                {
                    // Collection: source.AsQueryable().Where(...).Select(i => new DynamicType { ... }).ToList()
                    var itemParam = Expression.Parameter(itemType, "i");
                    var itemInit = BuildMemberInit(itemParam, itemType, childNode, childFilterContext, options);
                    var selectLambda = Expression.Lambda(itemInit, itemParam);

                    var asQueryableMethod = _queryableAsQueryable1.MakeGenericMethod(itemType);
                    var selectMethod = _queryableSelect2.MakeGenericMethod(itemType, itemInit.Type);
                    var toListMethod = _enumerableToList1.MakeGenericMethod(itemInit.Type);

                    var asQueryableCall = Expression.Call(null, asQueryableMethod, propAccess);
                    var maybeWhereCall = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(
                        asQueryableCall,
                        itemType,
                        childFilterContext,
                        options);

                    var selectCall = Expression.Call(null, selectMethod, maybeWhereCall, selectLambda);
                    var toListCall = Expression.Call(null, toListMethod, selectCall);

                    var targetListType = typeof(List<>).MakeGenericType(itemInit.Type);
                    // Collections always use the nav property name (alias not meaningful on the collection wrapper itself)
                    propertiesToSelect[outputName] = (targetListType, toListCall);
                }
                else
                {
                    // Nested Object
                    var nestedInit = BuildMemberInit(propAccess, propType, childNode, childFilterContext, options);
                    var isNullable = !propType.IsValueType || Nullable.GetUnderlyingType(propType) != null;

                    if (isNullable)
                    {
                        var nullCheck = Expression.Equal(propAccess, Expression.Constant(null, propType));
                        var condition = Expression.Condition(
                            nullCheck,
                            Expression.Constant(null, nestedInit.Type),
                            nestedInit,
                            nestedInit.Type);

                        propertiesToSelect[outputName] = (nestedInit.Type, condition);
                    }
                    else
                    {
                        propertiesToSelect[outputName] = (nestedInit.Type, nestedInit);
                    }
                }
            }
            else
            {
                // Leaf scalar: apply alias as output name
                propertiesToSelect[outputName] = (propType, propAccess);
            }
        }

        if (propertiesToSelect.Count == 0)
        {
            var emptyType = DynamicTypeBuilder.GetDynamicType(new Dictionary<string, Type>());
            return Expression.New(emptyType);
        }

        var dynamicType = DynamicTypeBuilder.GetDynamicType(propertiesToSelect.ToDictionary(p => p.Key, p => p.Value.TargetType));
        var newExpr = Expression.New(dynamicType);

        var bindings = propertiesToSelect.Select(p =>
        {
            var targetProp = dynamicType.GetProperty(p.Key)!;
            return Expression.Bind(targetProp, p.Value.Assignment);
        });

        return Expression.MemberInit(newExpr, bindings);
    }

    private static FilterGroup? MergeFilters(FilterGroup? a, FilterGroup? b)
    {
        if (a == null) return b;
        if (b == null) return a;

        return new FilterGroup
        {
            Logic   = LogicOperator.And,
            Groups  = [a, b]
        };
    }

    private static bool IsIEnumerable(Type type, out Type itemType)
    {
        return Security.SafePropertyResolver.TryGetCollectionElementType(type, out itemType);
    }

    private static SelectionNode NormalizeSelection(Type sourceType, SelectionNode selectTree)
    {
        var effective = new SelectionNode();

        if (selectTree.IncludeAllScalars)
        {
            foreach (var prop in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsScalarType(prop.PropertyType))
                {
                    effective.GetOrAddChild(prop.Name);
                }
            }
        }

        foreach (var child in selectTree.EnumerateChildren())
        {
            var effectiveChild = effective.GetOrAddChild(child.Key);
            effectiveChild.Filter = child.Value.Filter;
            // Preserve alias from the original node
            if (!string.IsNullOrWhiteSpace(child.Value.Alias))
                effectiveChild.Alias = child.Value.Alias;
            MergeNodes(effectiveChild, child.Value);
        }

        if (!effective.HasChildren && !selectTree.HasChildren)
        {
            foreach (var prop in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsScalarType(prop.PropertyType))
                {
                    effective.GetOrAddChild(prop.Name);
                }
            }
        }

        return effective;
    }

    private static bool ShouldBuildNestedProjection(Type propertyType, SelectionNode node)
    {
        if (IsIEnumerable(propertyType, out _))
        {
            return node.IncludeAllScalars || node.HasChildren;
        }

        return !IsScalarType(propertyType) && (node.IncludeAllScalars || node.HasChildren);
    }

    private static void MergeNodes(SelectionNode target, SelectionNode source)
    {
        if (source.IncludeAllScalars)
        {
            target.MarkIncludeAllScalars();
        }

        if (source.Filter != null)
        {
            target.Filter = source.Filter;
        }

        // Preserve alias
        if (!string.IsNullOrWhiteSpace(source.Alias))
        {
            target.Alias = source.Alias;
        }

        foreach (var child in source.EnumerateChildren())
        {
            var targetChild = target.GetOrAddChild(child.Key);
            MergeNodes(targetChild, child.Value);
        }
    }

    private static bool IsScalarType(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        return unwrapped.IsPrimitive
            || unwrapped.IsEnum
            || unwrapped == typeof(string)
            || unwrapped == typeof(decimal)
            || unwrapped == typeof(DateTime)
            || unwrapped == typeof(DateTimeOffset)
            || unwrapped == typeof(Guid)
            || unwrapped == typeof(TimeSpan)
            || unwrapped == typeof(DateOnly)
            || unwrapped == typeof(TimeOnly);
    }

    private static string GenerateCacheKey(SelectionNode tree)
    {
        if (!tree.HasChildren && !tree.IncludeAllScalars) return "*";

        var keys = tree.EnumerateChildren()
            .OrderBy(k => k.Key)
            .Select(k => $"{k.Key}@{k.Value.Alias ?? ""}:{GenerateCacheKey(k.Value)}|F:{FilterAnalyzer.CacheKey(k.Value.Filter)}");

        var scalarMarker = tree.IncludeAllScalars ? "!" : string.Empty;
        var payload = string.Join(",", keys);
        if (string.IsNullOrEmpty(payload))
        {
            return scalarMarker;
        }

        return scalarMarker + "(" + payload + ")";
    }

    /// <summary>
    /// Builds a projection expression from a list of dot-notation field paths.
    /// Used by ProjectionExpressionCache for caching projections by field list.
    /// </summary>
    public static Expression BuildFromSelectionFields(
        Type entityType,
        IReadOnlyList<string> selectionFields,
        QueryOptions options)
    {
        var tree = new SelectionNode();

        foreach (var field in selectionFields)
        {
            MergeFieldPath(tree, field);
        }

        var param = Expression.Parameter(entityType, "x");
        var memberInit = BuildMemberInit(param, entityType, tree, options.Filter, options);
        return Expression.Convert(memberInit, typeof(object));
    }

    private static void MergeFieldPath(SelectionNode current, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Handle alias
        var aliasParts = System.Text.RegularExpressions.Regex.Split(path, @"\s+as\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var actualPath = aliasParts[0].Trim();
        var alias = aliasParts.Length > 1 ? aliasParts[1].Trim() : null;

        var parts = actualPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var node = current;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            node = node.GetOrAddChild(part);
            if (i == parts.Length - 1 && alias != null)
            {
                node.Alias = alias;
            }
        }
    }
}
