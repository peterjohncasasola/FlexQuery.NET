using System.Linq.Expressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Helpers;

namespace DynamicQueryable.Security;

internal static class SafeConditionBuilder
{
    public static Expression? Build(Expression member, string op, string? rawValue)
    {
        if (op == FilterOperators.IsNull) return BuildNull(member, true);
        if (op == FilterOperators.IsNotNull) return BuildNull(member, false);
        if (op == FilterOperators.Contains) return BuildString(member, rawValue, nameof(string.Contains));
        if (op == FilterOperators.StartsWith) return BuildString(member, rawValue, nameof(string.StartsWith));
        if (op == FilterOperators.EndsWith) return BuildString(member, rawValue, nameof(string.EndsWith));
        if (op == FilterOperators.In) return BuildIn(member, rawValue);
        if (op == FilterOperators.NotIn)
        {
            var inExpr = BuildIn(member, rawValue);
            return inExpr is null ? null : Expression.Not(inExpr);
        }

        if (op == FilterOperators.Between) return BuildBetween(member, rawValue);

        if (!OperatorRegistry.BinaryFactories.TryGetValue(op, out var factory))
            return null;

        var converted = TypeHelper.ConvertValue(rawValue, member.Type);
        if (converted is null && rawValue is not null) return null;

        var constant = Expression.Constant(converted, member.Type);
        if (IsComparisonOperator(op) && !IsComparable(member.Type)) return null;
        return factory(member, constant);
    }

    private static Expression BuildNull(Expression member, bool isNull)
    {
        var type = member.Type;
        var canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        if (!canBeNull) return Expression.Constant(!isNull);

        var nullConstant = Expression.Constant(null, type);
        return isNull ? Expression.Equal(member, nullConstant) : Expression.NotEqual(member, nullConstant);
    }

    private static Expression? BuildString(Expression member, string? value, string methodName)
    {
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        if (underlying != typeof(string)) return null;

        var method = typeof(string).GetMethod(methodName, [typeof(string)]);
        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
        if (method is null || toLower is null) return null;

        var memberLower = Expression.Call(member, toLower);
        var valueLower = Expression.Constant((value ?? string.Empty).ToLowerInvariant());
        return Expression.Call(memberLower, method, valueLower);
    }

    private static Expression? BuildIn(Expression member, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Expression.Constant(false);

        var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parts = values
            .Select(v => TypeHelper.ConvertValue(v, member.Type))
            .Where(v => v is not null)
            .Select(v => Expression.Equal(member, Expression.Constant(v, member.Type)))
            .Cast<Expression>()
            .ToList();

        return parts.Count == 0 ? Expression.Constant(false) : parts.Aggregate(Expression.OrElse);
    }

    private static Expression? BuildBetween(Expression member, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !IsComparable(member.Type)) return null;

        var bounds = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bounds.Length != 2) return null;

        var lower = TypeHelper.ConvertValue(bounds[0], member.Type);
        var upper = TypeHelper.ConvertValue(bounds[1], member.Type);
        if (lower is null || upper is null) return null;

        var lowerExpr = Expression.GreaterThanOrEqual(member, Expression.Constant(lower, member.Type));
        var upperExpr = Expression.LessThanOrEqual(member, Expression.Constant(upper, member.Type));
        return Expression.AndAlso(lowerExpr, upperExpr);
    }

    private static bool IsComparisonOperator(string op)
        => op is FilterOperators.GreaterThan
            or FilterOperators.GreaterThanOrEq
            or FilterOperators.LessThan
            or FilterOperators.LessThanOrEq;

    private static bool IsComparable(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return TypeHelper.IsNumeric(t)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(DateOnly)
            || t == typeof(TimeOnly);
    }
}
