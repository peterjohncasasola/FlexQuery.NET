using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Projection;
using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Builders;

internal static class ProjectionBuilder
{
    private static readonly MethodInfo AsQueryable = ExpressionMethodCache.QueryableAsQueryable();
    private static readonly MethodInfo SelectMethod = ExpressionMethodCache.QueryableSelectSimple();
    private static readonly MethodInfo EnumerableToList = ExpressionMethodCache.EnumerableToList();

    public static Expression<Func<T, object>> BuildExpression<T>(
        SelectionNode selectTree,
        QueryOptions options)
    {
        if (QueryCacheManager.ShouldCache(options.EnableCache)
            && QueryCacheKeyBuilder.CanCache(options))
        {
            var cacheKey = QueryCacheKeyBuilder.Build(options, typeof(T), "projection") + ":" + GenerateCacheKey(selectTree);
            return QueryCacheManager.GetOrAddExpression(cacheKey, () =>
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

    private static Expression BuildMemberInit(
        Expression source,
        Type sourceType,
        SelectionNode selectTree,
        FilterGroup? filterContext,
        QueryOptions options,
        bool isRoot = true)
    {
        // For collection navigation properties, this builds a projection expression
        // using AsQueryable() + Select() + Enumerable.ToList(). EF Core 6+ translates
        // this pattern into correlated subqueries or split queries. EF Core 3.1 will
        // throw InvalidOperationException (client evaluation is blocked by default)
        // because Enumerable.ToList() cannot be translated by that version.

        var governedSelectFields = isRoot ? options.Select : null;
        var effectiveNode = ProjectionMetadataBuilder.NormalizeSelection(sourceType, selectTree, governedSelectFields);

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
                var propInfo = ReflectionCache.GetProperty(sourceType, propName);
                if (propInfo == null) continue;
                propAccess = Expression.Property(source, propInfo);
                propType = propInfo.PropertyType;
            }

            if (ProjectionMetadataBuilder.ShouldBuildNestedProjection(propType, childNode))
            {
                var childFilterContext = MergeFilters(
                    filterContext != null ? FilterAnalyzer.ExtractForNavigation(filterContext!, propName) : null,
                    childNode.Filter);

                if (ProjectionMetadataBuilder.IsIEnumerable(propType, out var itemType))
                {
                    var itemParam = Expression.Parameter(itemType, "i");
                    var itemInit = BuildMemberInit(itemParam, itemType, childNode, childFilterContext, options, isRoot: false);
                    var selectLambda = Expression.Lambda(itemInit, itemParam);

                    var asQueryableMethod = AsQueryable.MakeGenericMethod(itemType);
                    var selectMethod = SelectMethod.MakeGenericMethod(itemType, itemInit.Type);
                    var toListMethod = EnumerableToList.MakeGenericMethod(itemInit.Type);

                    var asQueryableCall = Expression.Call(null, asQueryableMethod, propAccess);
                    var maybeWhereCall = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(
                        asQueryableCall,
                        itemType,
                        childFilterContext,
                        options);

                    var selectCall = Expression.Call(null, selectMethod, maybeWhereCall, selectLambda);
                    var toListCall = Expression.Call(null, toListMethod, selectCall);

                    var targetListType = typeof(List<>).MakeGenericType(itemInit.Type);
                    propertiesToSelect[outputName] = (targetListType, toListCall);
                }
                else
                {
                    var nestedInit = BuildMemberInit(propAccess, propType, childNode, childFilterContext, options, isRoot: false);
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
                propertiesToSelect[outputName] = (propType, propAccess);
            }
        }

        if (propertiesToSelect.Count == 0)
        {
            var emptyType = DynamicTypeBuilder.GetDynamicType(new Dictionary<string, Type>());
            return Expression.New(emptyType);
        }

        var dynamicType = DynamicTypeBuilder.GetDynamicType(
            propertiesToSelect.ToDictionary(p => p.Key, p => p.Value.TargetType));
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
            Logic = LogicOperator.And,
            Groups = [a, b]
        };
    }

    private static void MergeFieldPath(SelectionNode current, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

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

    private static string GenerateCacheKey(SelectionNode tree)
    {
        if (!tree.HasChildren && !tree.IncludeAllScalars) return "*";

        var keys = tree.EnumerateChildren()
            .OrderBy(k => k.Key)
            .Select(k => $"{k.Key}@{k.Value.Alias ?? ""}:{GenerateCacheKey(k.Value)}|F:{FilterNormalizer.GenerateCacheKey(k.Value.Filter)}");

        var scalarMarker = tree.IncludeAllScalars ? "!" : string.Empty;
        var payload = string.Join(",", keys);
        if (string.IsNullOrEmpty(payload))
        {
            return scalarMarker;
        }

        return scalarMarker + "(" + payload + ")";
    }
}
