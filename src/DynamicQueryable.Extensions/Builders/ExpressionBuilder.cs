using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Constants;
using DynamicQueryable.Helpers;
using DynamicQueryable.Models;
using DynamicQueryable.Security;

namespace DynamicQueryable.Builders;

/// <summary>
/// Builds strongly-typed LINQ Expression trees from <see cref="FilterGroup"/> and
/// <see cref="FilterCondition"/> objects. All expression building is done without
/// string-eval so it is EF Core-translatable.
/// </summary>
public static class ExpressionBuilder
{
    /// <summary>
    /// Builds a combined predicate expression for the given <see cref="FilterGroup"/>.
    /// Returns null if the group is empty (caller should skip the Where clause).
    /// </summary>
    public static Expression<Func<T, bool>>? BuildPredicate<T>(FilterGroup group)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var body = BuildGroupExpression(param, group, typeof(T));
        if (body is null) return null;

        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    // ── Internal recursion ───────────────────────────────────────────────

    private static Expression? BuildGroupExpression(
        ParameterExpression param, FilterGroup group, Type entityType)
    {
        var parts = new List<Expression>();

        // Leaf filter conditions
        foreach (var condition in group.Filters)
        {
            var expr = BuildConditionExpression(param, condition, entityType);
            if (expr is not null) parts.Add(expr);
        }

        // Nested sub-groups
        foreach (var subGroup in group.Groups)
        {
            var subExpr = BuildGroupExpression(param, subGroup, entityType);
            if (subExpr is not null) parts.Add(subExpr);
        }

        if (parts.Count == 0) return null;

        var result = parts.Aggregate((left, right) =>
            group.Logic == LogicOperator.Or
                ? Expression.OrElse(left, right)
                : Expression.AndAlso(left, right));

        return group.IsNegated ? Expression.Not(result) : result;
    }

    private static Expression? BuildConditionExpression(
        ParameterExpression param, FilterCondition condition, Type entityType)
    {
        if (string.IsNullOrWhiteSpace(condition.Field)) return null;
        var op = FilterOperators.Normalize(condition.Operator);
        if (!OperatorRegistry.IsAllowed(op)) return null;
        if (!FieldRegistry.IsAllowed(entityType, condition.Field)) return null;
        if (!SafePropertyResolver.TryResolveChain(entityType, condition.Field, out var chain)) return null;

        Expression? expression = op switch
        {
            FilterOperators.Any => BuildAnyExpression(param, chain, condition.Value),
            FilterOperators.Count => BuildCountExpression(param, chain, condition.Value),
            _ => BuildPathExpression(param, chain, 0, op, condition.Value)
        };

        if (expression is null) return null;
        return condition.IsNegated ? Expression.Not(expression) : expression;
    }

    private static Expression? BuildPathExpression(
        Expression current,
        IReadOnlyList<PropertyInfo> chain,
        int index,
        string op,
        string? rawValue)
    {
        var prop = chain[index];
        var access = Expression.Property(current, prop);
        var isLeaf = index == chain.Count - 1;
        if (isLeaf)
        {
            return SafeConditionBuilder.Build(access, op, rawValue);
        }

        if (SafePropertyResolver.TryGetCollectionElementType(prop.PropertyType, out var elementType))
        {
            var itemParam = Expression.Parameter(elementType, $"i{index}");
            var predicate = BuildPathExpression(itemParam, chain, index + 1, op, rawValue);
            if (predicate is null) return null;

            var anyMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.Any)
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);

            return Expression.Call(
                anyMethod,
                access,
                Expression.Lambda(predicate, itemParam));
        }

        return BuildPathExpression(access, chain, index + 1, op, rawValue);
    }

    private static Expression? BuildAnyExpression(
        Expression param,
        IReadOnlyList<PropertyInfo> chain,
        string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var segments = rawValue.Split(':', 3, StringSplitOptions.TrimEntries);
        if (segments.Length != 3) return null;

        var nestedField = segments[0];
        var nestedOperator = FilterOperators.Normalize(segments[1]);
        var nestedValue = segments[2];

        var collectionAccess = ResolvePath(param, chain, out var collectionType);
        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        if (!SafePropertyResolver.TryResolveChain(elementType, nestedField, out var nestedChain)) return null;

        var itemParam = Expression.Parameter(elementType, "c");
        var predicate = BuildPathExpression(itemParam, nestedChain, 0, nestedOperator, nestedValue);
        if (predicate is null) return null;

        var anyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(anyMethod, collectionAccess, Expression.Lambda(predicate, itemParam));
    }

    private static Expression? BuildCountExpression(
        Expression param,
        IReadOnlyList<PropertyInfo> chain,
        string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;
        var segments = rawValue.Split(':', 2, StringSplitOptions.TrimEntries);
        if (segments.Length != 2) return null;

        var comparisonOp = FilterOperators.Normalize(segments[0]);
        if (!OperatorRegistry.BinaryFactories.TryGetValue(comparisonOp, out var binaryFactory)) return null;

        var converted = TypeHelper.ConvertValue(segments[1], typeof(int));
        if (converted is not int countValue) return null;

        var collectionAccess = ResolvePath(param, chain, out var collectionType);
        if (collectionAccess is null || collectionType is null) return null;
        if (!SafePropertyResolver.TryGetCollectionElementType(collectionType, out var elementType)) return null;

        var countMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1)
            .MakeGenericMethod(elementType);

        var countCall = Expression.Call(countMethod, collectionAccess);
        return binaryFactory(countCall, Expression.Constant(countValue, typeof(int)));
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
