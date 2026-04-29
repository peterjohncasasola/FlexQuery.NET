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

        return parts.Aggregate((left, right) =>
            group.Logic == LogicOperator.Or
                ? Expression.OrElse(left, right)
                : Expression.AndAlso(left, right));
    }

    private static Expression? BuildConditionExpression(
        ParameterExpression param, FilterCondition condition, Type entityType)
    {
        if (string.IsNullOrWhiteSpace(condition.Field)) return null;
        var op = FilterOperators.Normalize(condition.Operator);
        if (!OperatorRegistry.IsAllowed(op)) return null;
        if (!FieldRegistry.IsAllowed(entityType, condition.Field)) return null;
        if (!SafePropertyResolver.TryResolveChain(entityType, condition.Field, out var chain)) return null;

        return BuildPathExpression(param, chain, 0, op, condition.Value);
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
}
