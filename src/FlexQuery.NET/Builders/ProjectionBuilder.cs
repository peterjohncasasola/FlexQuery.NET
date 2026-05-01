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
    private static readonly ConcurrentDictionary<string, Expression> _cache = new();

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

    public static Expression<Func<T, object>> Build<T>(SelectionNode selectTree, FilterGroup? filter = null)
    {
        var cacheKey = typeof(T).FullName + ":" + GenerateCacheKey(selectTree) + "|F:" + FilterAnalyzer.CacheKey(filter);

        return (Expression<Func<T, object>>)_cache.GetOrAdd(cacheKey, _ =>
        {
            var param = Expression.Parameter(typeof(T), "x");
            var memberInit = BuildMemberInit(param, typeof(T), selectTree, filter);
            var boxed = Expression.Convert(memberInit, typeof(object));
            return Expression.Lambda<Func<T, object>>(boxed, param);
        });
    }

    private static Expression BuildMemberInit(
        Expression source,
        Type sourceType,
        SelectionNode selectTree,
        FilterGroup? filterContext)
    {
        var effectiveNode = NormalizeSelection(sourceType, selectTree);

        // Key = output name (alias or prop name), Value = (clrType, expression)
        var propertiesToSelect = new Dictionary<string, (Type TargetType, Expression Assignment)>();

        foreach (var kvp in effectiveNode.EnumerateChildren())
        {
            var propInfo = sourceType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propInfo == null) continue;

            var childNode = kvp.Value;
            // Output name: use alias if set, otherwise the CLR property name
            var outputName = !string.IsNullOrWhiteSpace(childNode.Alias) ? childNode.Alias : propInfo.Name;

            var propAccess = Expression.Property(source, propInfo);

            if (ShouldBuildNestedProjection(propInfo.PropertyType, childNode))
            {
                var childFilterContext = MergeFilters(
                    filterContext != null ? FilterAnalyzer.ExtractForNavigation(filterContext, propInfo.Name) : null,
                    childNode.Filter);

                if (IsIEnumerable(propInfo.PropertyType, out var itemType))
                {
                    // Collection: source.AsQueryable().Where(...).Select(i => new DynamicType { ... }).ToList()
                    var itemParam = Expression.Parameter(itemType, "i");
                    var itemInit = BuildMemberInit(itemParam, itemType, childNode, childFilterContext);
                    var selectLambda = Expression.Lambda(itemInit, itemParam);

                    var asQueryableMethod = _queryableAsQueryable1.MakeGenericMethod(itemType);
                    var selectMethod = _queryableSelect2.MakeGenericMethod(itemType, itemInit.Type);
                    var toListMethod = _enumerableToList1.MakeGenericMethod(itemInit.Type);

                    var asQueryableCall = Expression.Call(null, asQueryableMethod, propAccess);
                    var maybeWhereCall = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(
                        asQueryableCall,
                        itemType,
                        childFilterContext);

                    var selectCall = Expression.Call(null, selectMethod, maybeWhereCall, selectLambda);
                    var toListCall = Expression.Call(null, toListMethod, selectCall);

                    var targetListType = typeof(List<>).MakeGenericType(itemInit.Type);
                    // Collections always use the nav property name (alias not meaningful on the collection wrapper itself)
                    propertiesToSelect[outputName] = (targetListType, toListCall);
                }
                else
                {
                    // Nested Object
                    var nestedInit = BuildMemberInit(propAccess, propInfo.PropertyType, childNode, childFilterContext);
                    var isNullable = !propInfo.PropertyType.IsValueType || Nullable.GetUnderlyingType(propInfo.PropertyType) != null;

                    if (isNullable)
                    {
                        var nullCheck = Expression.Equal(propAccess, Expression.Constant(null, propInfo.PropertyType));
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
                propertiesToSelect[outputName] = (propInfo.PropertyType, propAccess);
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
}
