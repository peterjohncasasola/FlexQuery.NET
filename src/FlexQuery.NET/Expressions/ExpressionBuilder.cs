using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Expressions;

/// <summary>
/// Builds strongly-typed LINQ Expression trees from <see cref="FilterGroupNode"/> and
/// <see cref="FilterCondition"/> objects. All expression building is done without
/// string-eval so it is EF Core-translatable.
/// </summary>
internal static class ExpressionBuilder
{
    /// <summary>
    /// Builds a combined predicate expression for the given <see cref="FilterGroupNode"/>.
    /// Returns null if the group is empty (caller should skip the Where clause).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="options">The query options containing the filter to build a predicate from.</param>
    /// <returns>An expression predicate, or null if the filter is empty.</returns>
    public static Expression<Func<T, bool>>? BuildPredicate<T>(QueryOptions options)
    {
        if (options.Filter is null) return null;

        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildGroupExpression(param, options.Filter!, typeof(T), options);
        if (body is null) return null;

        var lambda = Expression.Lambda<Func<T, bool>>(body, param);

        if (QueryCacheManager.ShouldCache(options.EnableCache)
            && QueryCacheKeyBuilder.CanCache(options))
        {
            var key = QueryCacheKeyBuilder.Build(options, typeof(T), "predicate");
            return QueryCacheManager.GetOrAddExpression(key, () => lambda);
        }

        return lambda;
    }

    /// <summary>
    /// Backward-compatible overload. Builds a predicate from a <see cref="FilterGroupNode"/>
    /// using default options (CaseInsensitive = true).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="group">The filter group node to build a predicate from.</param>
    /// <returns>An expression predicate, or null if the group is empty.</returns>
    public static Expression<Func<T, bool>>? BuildPredicate<T>(FilterGroupNode? group)
        => BuildPredicate<T>(new QueryOptions { Filter = group });

    /// <summary>
    /// Runtime-type variant used by the Include Pipeline when the element
    /// type is only known via reflection. Returns a <see cref="LambdaExpression"/>
    /// whose delegate type is <c>Func&lt;elementType, bool&gt;</c>.
    /// </summary>
    /// <param name="elementType">The runtime type of the collection element.</param>
    /// <param name="options">The query options containing the filter to build a predicate from.</param>
    /// <returns>A lambda expression predicate, or null if the filter is empty.</returns>
    public static LambdaExpression? BuildPredicate(Type elementType, QueryOptions options)
    {
        if (options.Filter is null) return null;

        var param = Expression.Parameter(elementType, "x");
        var body = BuildGroupExpression(param, options.Filter!, elementType, options);
        if (body is null) return null;

        var lambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(elementType, typeof(bool)),
            body,
            param);

        if (QueryCacheManager.ShouldCache(options.EnableCache)
            && QueryCacheKeyBuilder.CanCache(options))
        {
            var key = QueryCacheKeyBuilder.Build(options, elementType, "predicate_dynamic");
            return QueryCacheManager.GetOrAddExpression(key, () => lambda);
        }

        return lambda;
    }

    /// <summary>
    /// Backward-compatible overload for the runtime-type variant.
    /// Uses default options (CaseInsensitive = true).
    /// </summary>
    /// <param name="elementType">The runtime type of the collection element.</param>
    /// <param name="group">The filter group node to build a predicate from.</param>
    /// <returns>A lambda expression predicate, or null if the group is empty.</returns>
    public static LambdaExpression? BuildPredicate(Type elementType, FilterGroupNode? group)
        => BuildPredicate(elementType, new QueryOptions { Filter = group });

    private static Expression? BuildGroupExpression(
        ParameterExpression param, FilterGroupNode group, Type entityType, QueryOptions options)
    {
        var parts = new List<Expression>();

        foreach (var child in group.Children)
        {
            var expr = child switch
            {
                FilterGroupNode g => BuildGroupExpression(param, g, entityType, options),
                FilterConditionNode c => BuildConditionExpression(param, c, entityType, options),
                _ => null
            };

            if (expr is not null) parts.Add(expr);
        }

        if (parts.Count == 0) return null;

        var result = parts.Aggregate((left, right) =>
            group.Logic == LogicOperator.Or
                ? Expression.OrElse(left, right)
                : Expression.AndAlso(left, right));

        return group.IsNegated ? Expression.Not(result) : result;
    }

    private static Expression? BuildConditionExpression(
        ParameterExpression param, FilterConditionNode condition, Type entityType, QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(condition.Field)) return null;
        var op = FilterOperators.Normalize(condition.Operator);
        if (!FilterOperators.IsSupported(op)) return null;
        if (!FieldRegistry.IsAllowed(entityType, condition.Field)) return null;

        if (FieldResolver.TryResolveMappedExpression(param, condition.Field, options, out var resolvedExpr, out var resolvedType))
        {
            if (condition.ScopedFilter is not null)
                return BuildScopedCollectionExpression(resolvedExpr, resolvedType, op, condition.ScopedFilter, options);

            Expression? expr = op switch
            {
                FilterOperators.Any => BuildAnyExpression(resolvedExpr, resolvedType, condition.Value, options),
                FilterOperators.All => BuildAllExpression(resolvedExpr, resolvedType, condition.Value, options),
                FilterOperators.Count => BuildCountExpression(resolvedExpr, resolvedType, condition.Value, options),
                _ => FilterExpressionBuilder.Build(resolvedExpr, op, condition.Value, options.CaseInsensitive)
            };

            if (expr is null) return null;
            return condition.IsNegated ? Expression.Not(expr) : expr;
        }

        if (!SafePropertyResolver.TryResolveChain(entityType, condition.Field, out var chain)) return null;

        if (condition.ScopedFilter is not null)
        {
            var collectionAccess = ResolvePath(param, chain, out var collectionType);
            return BuildScopedCollectionExpression(collectionAccess, collectionType, op, condition.ScopedFilter, options);
        }

        Expression? expression = op switch
        {
            FilterOperators.Any => BuildAnyExpression(ResolvePath(param, chain, out var t1), t1, condition.Value, options),
            FilterOperators.All => BuildAllExpression(ResolvePath(param, chain, out var t2), t2, condition.Value, options),
            FilterOperators.Count => BuildCountExpression(ResolvePath(param, chain, out var t3), t3, condition.Value, options),
            _ => BuildPathExpression(param, chain, 0, op, condition.Value, options)
        };

        if (expression is null) return null;
        return condition.IsNegated ? Expression.Not(expression) : expression;
    }

    private static Expression? BuildScopedCollectionExpression(
        Expression? collectionAccess,
        Type? collectionType,
        string quantifier,
        FilterGroupNode scopedFilter,
        QueryOptions options)
    {
        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        var itemParam = Expression.Parameter(elementType, "sc");
        var predicate = BuildGroupExpression(itemParam, scopedFilter, elementType, options);
        if (predicate is null) return null;

        if (quantifier == FilterOperators.All)
        {
            var allMethod = ExpressionMethodCache.EnumerableAll(elementType);

            var allCall = Expression.Call(allMethod, collectionAccess, Expression.Lambda(predicate, itemParam));
            var isNull = Expression.Equal(collectionAccess, Expression.Constant(null, collectionType));
            return Expression.OrElse(isNull, allCall);
        }

        var anyMethod = ExpressionMethodCache.EnumerableAnyWithPredicate(elementType);

        var anyCall = Expression.Call(anyMethod, collectionAccess, Expression.Lambda(predicate, itemParam));
        return anyCall;
    }

    private static Expression? BuildPathExpression(
        Expression current,
        IReadOnlyList<PropertyInfo> chain,
        int index,
        string op,
        string? rawValue,
        QueryOptions options)
    {
        var prop = chain[index];
        var access = Expression.Property(current, prop);
        var isLeaf = index == chain.Count - 1;
        if (isLeaf)
        {
            return FilterExpressionBuilder.Build(access, op, rawValue, options.CaseInsensitive);
        }

        if (SafePropertyResolver.TryGetCollectionElementType(prop.PropertyType, out var elementType))
        {
            var itemParam = Expression.Parameter(elementType, $"i{index}");
            var predicate = BuildPathExpression(itemParam, chain, index + 1, op, rawValue, options);
            if (predicate is null) return null;

            var anyMethod = ExpressionMethodCache.EnumerableAnyWithPredicate(elementType);

            var anyCall = Expression.Call(
                anyMethod,
                access,
                Expression.Lambda(predicate, itemParam));

            return anyCall;
        }

        return BuildPathExpression(access, chain, index + 1, op, rawValue, options);
    }

    private static Expression? BuildAnyExpression(
        Expression? collectionAccess,
        Type? collectionType,
        string? rawValue,
        QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var segments = rawValue.Split(':', 3, StringSplitOptions.TrimEntries);
        if (segments.Length != 3) return null;

        var nestedField = segments[0];
        var nestedOperator = FilterOperators.Normalize(segments[1]);
        var nestedValue = segments[2];

        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        if (!SafePropertyResolver.TryResolveChain(elementType, nestedField, out var nestedChain)) return null;

        var itemParam = Expression.Parameter(elementType, "c");
        var predicate = BuildPathExpression(itemParam, nestedChain, 0, nestedOperator, nestedValue, options);
        if (predicate is null) return null;

        var anyMethod = ExpressionMethodCache.EnumerableAnyWithPredicate(elementType);

        var anyCall = Expression.Call(anyMethod, collectionAccess, Expression.Lambda(predicate, itemParam));
        return anyCall;
    }

    private static Expression? BuildAllExpression(
        Expression? collectionAccess,
        Type? collectionType,
        string? rawValue,
        QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var segments = rawValue.Split(':', 3, StringSplitOptions.TrimEntries);
        if (segments.Length != 3) return null;

        var nestedField = segments[0];
        var nestedOperator = FilterOperators.Normalize(segments[1]);
        var nestedValue = segments[2];

        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        if (!SafePropertyResolver.TryResolveChain(elementType, nestedField, out var nestedChain)) return null;

        var itemParam = Expression.Parameter(elementType, "c");
        var predicate = BuildPathExpression(itemParam, nestedChain, 0, nestedOperator, nestedValue, options);
        if (predicate is null) return null;

        var allMethod = ExpressionMethodCache.EnumerableAll(elementType);

        var allCall = Expression.Call(allMethod, collectionAccess, Expression.Lambda(predicate, itemParam));
        var isNull = Expression.Equal(collectionAccess, Expression.Constant(null, collectionType));
        return Expression.OrElse(isNull, allCall);
    }

    private static Expression? BuildCountExpression(
        Expression? collectionAccess,
        Type? collectionType,
        string? rawValue,
        QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;
        var segments = rawValue.Split(':', 2, StringSplitOptions.TrimEntries);
        if (segments.Length != 2) return null;

        var comparisonOp = FilterOperators.Normalize(segments[0]);
        if (!OperatorRegistry.BinaryFactories.TryGetValue(comparisonOp, out var binaryFactory)) return null;

        var converted = TypeHelper.ConvertValue(segments[1], typeof(int));
        if (converted is not int countValue) return null;

        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        var countMethod = ExpressionMethodCache.EnumerableCount(elementType);

        var countCall = Expression.Call(countMethod, collectionAccess);
        var compareExpr = binaryFactory(countCall, Expression.Constant(countValue, typeof(int)));

        var nullCheck = Expression.NotEqual(collectionAccess, Expression.Constant(null, collectionType));
        var zeroCompare = binaryFactory(Expression.Constant(0, typeof(int)), Expression.Constant(countValue, typeof(int)));

        return Expression.Condition(nullCheck, compareExpr, zeroCompare);
    }

    private static Expression? ResolvePath(
        Expression start,
        IReadOnlyList<PropertyInfo> chain,
        out Type? resolvedType)
    {
        var current = start;
        resolvedType = null;

        foreach (var propertyInfo in chain)
        {
            current = Expression.Property(current, propertyInfo);
            resolvedType = propertyInfo.PropertyType;
        }

        return current;
    }
}
