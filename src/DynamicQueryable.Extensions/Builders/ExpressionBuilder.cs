using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Constants;
using DynamicQueryable.Helpers;
using DynamicQueryable.Models;

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

        // Resolve potentially dot-separated property chain
        var memberAccess = ResolveMemberAccess(param, condition.Field, entityType);
        if (memberAccess is null) return null;

        var propertyType = memberAccess.Type;
        var op = FilterOperators.Normalize(condition.Operator);

        // Null-check operators don't need a value
        if (op == FilterOperators.IsNull)
            return BuildIsNullExpression(memberAccess, propertyType, isNull: true);
        if (op == FilterOperators.IsNotNull)
            return BuildIsNullExpression(memberAccess, propertyType, isNull: false);

        // String operators and 'In' handle their own value parsing — bypass single-value conversion
        if (op == FilterOperators.Contains)   return BuildStringMethod(memberAccess, condition.Value, nameof(string.Contains));
        if (op == FilterOperators.StartsWith) return BuildStringMethod(memberAccess, condition.Value, nameof(string.StartsWith));
        if (op == FilterOperators.EndsWith)   return BuildStringMethod(memberAccess, condition.Value, nameof(string.EndsWith));
        if (op == FilterOperators.In)         return BuildInExpression(memberAccess, condition.Value, propertyType);

        // Convert the raw string value to the property type for all remaining operators
        var converted = TypeHelper.ConvertValue(condition.Value, propertyType);
        if (converted is null && condition.Value is not null) return null; // type mismatch — ignore

        var constant = Expression.Constant(converted, propertyType);

        return op switch
        {
            FilterOperators.Equal           => Expression.Equal(memberAccess, constant),
            FilterOperators.NotEqual        => Expression.NotEqual(memberAccess, constant),
            FilterOperators.GreaterThan     => BuildCompare(memberAccess, constant, propertyType, ExpressionType.GreaterThan),
            FilterOperators.GreaterThanOrEq => BuildCompare(memberAccess, constant, propertyType, ExpressionType.GreaterThanOrEqual),
            FilterOperators.LessThan        => BuildCompare(memberAccess, constant, propertyType, ExpressionType.LessThan),
            FilterOperators.LessThanOrEq    => BuildCompare(memberAccess, constant, propertyType, ExpressionType.LessThanOrEqual),
            _                               => Expression.Equal(memberAccess, constant)
        };
    }

    // ── Member access resolution ─────────────────────────────────────────

    private static MemberExpression? ResolveMemberAccess(
        Expression root, string fieldPath, Type entityType)
    {
        var segments = fieldPath.Split('.');
        Expression current = root;
        Type currentType = entityType;

        foreach (var segment in segments)
        {
            var prop = currentType.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null) return null;

            current = Expression.Property(current, prop);
            currentType = prop.PropertyType;
        }

        return current as MemberExpression;
    }

    // ── Operator helpers ─────────────────────────────────────────────────

    private static Expression? BuildCompare(
        Expression member, Expression constant, Type propertyType, ExpressionType exprType)
    {
        // Only numeric/DateTime/comparable types support GT/LT
        if (!TypeHelper.IsNumeric(propertyType) &&
            propertyType != typeof(DateTime) &&
            propertyType != typeof(DateTimeOffset) &&
            propertyType != typeof(DateOnly) &&
            propertyType != typeof(TimeOnly))
            return null;

        return exprType switch
        {
            ExpressionType.GreaterThan        => Expression.GreaterThan(member, constant),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(member, constant),
            ExpressionType.LessThan           => Expression.LessThan(member, constant),
            ExpressionType.LessThanOrEqual    => Expression.LessThanOrEqual(member, constant),
            _                                 => null
        };
    }

    private static Expression? BuildStringMethod(
        Expression member, string? value, string methodName)
    {
        // Only applicable to string properties
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        if (underlying != typeof(string)) return null;

        var method = typeof(string).GetMethod(methodName, [typeof(string)]);
        if (method is null) return null;

        var constExpr = Expression.Constant(value ?? string.Empty);

        // Case-insensitive wrapper: member.ToLower().Contains(value.ToLower())
        var toLower   = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var memberLow = Expression.Call(member, toLower);
        var valueLow  = Expression.Constant((value ?? string.Empty).ToLowerInvariant());

        return Expression.Call(memberLow, method, valueLow);
    }

    private static Expression BuildIsNullExpression(
        Expression member, Type propertyType, bool isNull)
    {
        var isNullableRef  = !propertyType.IsValueType;
        var isNullableVal  = Nullable.GetUnderlyingType(propertyType) is not null;

        if (isNullableRef || isNullableVal)
        {
            var nullConst = Expression.Constant(null, propertyType);
            return isNull
                ? Expression.Equal(member, nullConst)
                : Expression.NotEqual(member, nullConst);
        }

        // Value types can never be null — return false/true constant
        return isNull ? Expression.Constant(false) : Expression.Constant(true);
    }

    private static Expression BuildInExpression(
        Expression member, string? rawValue, Type propertyType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return Expression.Constant(false);

        var values = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expressions = values
            .Select(v => TypeHelper.ConvertValue(v, propertyType))
            .Where(v => v is not null)
            .Select(v => Expression.Equal(member, Expression.Constant(v, propertyType)))
            .Cast<Expression>()
            .ToList();

        if (expressions.Count == 0) return Expression.Constant(false);
        return expressions.Aggregate(Expression.OrElse);
    }
}
