using System.Linq.Expressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Helpers;
using DynamicQueryable.Operators;

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
        if (member.Type == typeof(string) && (op == FilterOperators.Equal || op == FilterOperators.NotEqual))
            return BuildStringEqual(member, rawValue, op == FilterOperators.Equal);
        
        if (OperatorHandlerRegistry.TryGet(op, out var handler))
            return handler?.Build(member, rawValue);
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

        var constant = Parameterize(converted, member.Type);
        if (IsComparisonOperator(op) && !IsComparable(member.Type)) return null;
        return factory(member, constant);
    }

    private static Expression BuildNull(Expression member, bool isNull)
    {
        var type = member.Type;
        var canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        if (!canBeNull) return Expression.Constant(!isNull);

        var nullConstant = Parameterize(null, type);
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
        var valueLower = Parameterize((value ?? string.Empty).ToLowerInvariant(), typeof(string));
        return Expression.Call(memberLower, method, valueLower);
    }

    private static Expression? BuildStringEqual(Expression member, string? value, bool isEqual)
    {
        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
        if (toLower is null) return null;

        var memberLower = Expression.Call(member, toLower);
        var valueLower = Parameterize((value ?? string.Empty).ToLowerInvariant(), typeof(string));
        
        return isEqual 
            ? Expression.Equal(memberLower, valueLower) 
            : Expression.NotEqual(memberLower, valueLower);
    }

    private static Expression? BuildIn(Expression member, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Expression.Constant(false);

        var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parts = values
            .Select(v => TypeHelper.ConvertValue(v, member.Type))
            .Where(v => v is not null)
            .Select(v => Expression.Equal(member, Parameterize(v, member.Type)))
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

        var lowerExpr = Expression.GreaterThanOrEqual(member, Parameterize(lower, member.Type));
        var upperExpr = Expression.LessThanOrEqual(member, Parameterize(upper, member.Type));
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

    /// <summary>
    /// Forces EF Core to parameterize the value by wrapping it in a closure-like class,
    /// rather than embedding it as an inline literal in the SQL query.
    /// </summary>
    private static Expression Parameterize(object? value, Type type)
    {
        if (value is null) return Expression.Constant(null, type);

        var wrapperType = typeof(ParameterWrapper<>).MakeGenericType(type);
        var wrapper = Activator.CreateInstance(wrapperType);
        wrapperType.GetProperty("Value")!.SetValue(wrapper, value);

        return Expression.Property(Expression.Constant(wrapper), "Value");
    }

    private class ParameterWrapper<T>
    {
        public T Value { get; set; } = default!;
    }
}
