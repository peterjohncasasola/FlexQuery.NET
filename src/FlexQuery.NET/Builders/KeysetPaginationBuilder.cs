using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

internal static class KeysetPaginationBuilder
{
    private static readonly MethodInfo OrderByMethod = ExpressionMethodCache.QueryableOrderBy();
    private static readonly MethodInfo OrderByDescendingMethod = ExpressionMethodCache.QueryableOrderByDescending();
    private static readonly MethodInfo ThenByMethod = ExpressionMethodCache.QueryableThenBy();
    private static readonly MethodInfo ThenByDescendingMethod = ExpressionMethodCache.QueryableThenByDescending();
    public static List<(LambdaExpression KeySelector, bool Descending)> BuildOrderingInfos<T>(List<SortNode> sorts)
    {
        if (sorts.Count == 0)
            throw new KeysetPaginationException("Keyset pagination requires at least one sort field.Provide a Sort expression or call .OrderBy() before .SeekAfter().");
        
        var result = new List<(LambdaExpression, bool)>(sorts.Count);
        var parameter = Expression.Parameter(typeof(T), "x");

        foreach (var sort in sorts)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            var prop = ReflectionCache.GetProperty(typeof(T), sort.Field);
            if (prop == null) continue;

            var keyExpr = Expression.Property(parameter, prop);
            var keySelector = Expression.Lambda(keyExpr, parameter);
            result.Add((keySelector, sort.Descending));
        }

        return result;
    }

    public static Expression<Func<T, bool>> BuildSeekPredicate<T>(
        IReadOnlyList<(LambdaExpression KeySelector, bool Descending)> orderings,
        IReadOnlyList<object?> cursorValues)
    {
        if (orderings.Count == 0)
            throw new KeysetPaginationException(
                "Keyset pagination requires at least one sort field.");

        if (cursorValues.Count != orderings.Count)
            throw new KeysetPaginationException(
                $"Cursor has {cursorValues.Count} value(s) but the query has {orderings.Count} ordering column(s). " +
                $"Provide a cursor with {orderings.Count} value(s) or use the correct SeekAfter overload.");

        for (var i = 0; i < cursorValues.Count; i++)
        {
            if (cursorValues[i] is not null) continue;
            
            var keyType = orderings[i].KeySelector.Body.Type;
            
            if (keyType.IsValueType && Nullable.GetUnderlyingType(keyType) is null)
            {
                throw new KeysetPaginationException(
                    $"Cursor value at position {i} is null but the corresponding key type '{keyType.Name}' is not nullable.");
            }
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = BuildOrChain(parameter, orderings, cursorValues, 0);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression BuildOrChain(
        ParameterExpression parameter,
        IReadOnlyList<(LambdaExpression KeySelector, bool Descending)> orderings,
        IReadOnlyList<object?> cursorValues,
        int index)
    {
        if (index >= orderings.Count)
            return Expression.Constant(false);

        var (keySelector, descending) = orderings[index];
        var cursorValue = cursorValues[index];
        var keyBody = RebindParameter(keySelector, parameter);
        var valueExpr = ToConstant(cursorValue, keyBody.Type);
        Expression comparison;

        if (keyBody.Type == typeof(string))
        {
            var compareMethod = typeof(string).GetMethod("Compare", [typeof(string), typeof(string)])!;
            var compareCall = Expression.Call(compareMethod, keyBody, valueExpr);
            comparison = descending
                ? Expression.LessThan(compareCall, Expression.Constant(0))
                : Expression.GreaterThan(compareCall, Expression.Constant(0));
        }
        else
        {
            var opType = descending ? ExpressionType.LessThan : ExpressionType.GreaterThan;
            comparison = Expression.MakeBinary(opType, keyBody, valueExpr);
        }

        if (index == orderings.Count - 1)
            return comparison;

        var equalExpr = Expression.Equal(keyBody, valueExpr);
        var restExpr = BuildOrChain(parameter, orderings, cursorValues, index + 1);
        return Expression.OrElse(comparison, Expression.AndAlso(equalExpr, restExpr));
    }

    private static ConstantExpression ToConstant(object? value, Type targetType)
    {
        switch (value)
        {
            case null when targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null:
                return Expression.Constant(null, typeof(Nullable<>).MakeGenericType(targetType));
            case null:
            case JsonElement { ValueKind: JsonValueKind.Null }:
                return Expression.Constant(null, targetType);
            case JsonElement je:
            {
                if (targetType == typeof(object))
                    targetType = je.ValueKind switch
                    {
                        JsonValueKind.Number when je.TryGetInt32(out _) => typeof(int),
                        JsonValueKind.Number => typeof(double),
                        JsonValueKind.True or JsonValueKind.False => typeof(bool),
                        _ => typeof(string)
                    };

                switch (je.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (targetType == typeof(int)) return Expression.Constant(je.GetInt32(), targetType);
                        if (targetType == typeof(long)) return Expression.Constant(je.GetInt64(), targetType);
                        if (targetType == typeof(double)) return Expression.Constant(je.GetDouble(), targetType);
                        if (targetType == typeof(float)) return Expression.Constant(je.GetSingle(), targetType);
                        if (targetType == typeof(decimal)) return Expression.Constant(je.GetDecimal(), targetType);
                        if (targetType == typeof(short)) return Expression.Constant(je.GetInt16(), targetType);
                        if (targetType == typeof(byte)) return Expression.Constant(je.GetByte(), targetType);
                        if (targetType == typeof(int?)) return Expression.Constant(je.GetInt32(), typeof(int));
                        if (targetType == typeof(long?)) return Expression.Constant(je.GetInt64(), typeof(long));
                        if (targetType == typeof(double?)) return Expression.Constant(je.GetDouble(), typeof(double));
                        if (targetType == typeof(float?)) return Expression.Constant(je.GetSingle(), typeof(float));
                        if (targetType == typeof(decimal?)) return Expression.Constant(je.GetDecimal(), typeof(decimal));
                        
                        return targetType == typeof(short?) ? 
                            Expression.Constant(je.GetInt16(), typeof(short)) 
                            : Expression.Constant(je.GetInt32(), targetType);

                    case JsonValueKind.String:
                        if (targetType == typeof(string)) return Expression.Constant(je.GetString(), targetType);
                        if (targetType == typeof(Guid)) return Expression.Constant(je.GetGuid(), targetType);
                        if (targetType == typeof(Guid?)) return Expression.Constant(je.GetGuid(), typeof(Guid));
                        if (targetType == typeof(DateTime)) return Expression.Constant(je.GetDateTime(), targetType);
                        if (targetType == typeof(DateTime?)) return Expression.Constant(je.GetDateTime(), typeof(DateTime));
                        if (targetType == typeof(DateTimeOffset)) return Expression.Constant(je.GetDateTimeOffset(), targetType);
                        if (targetType == typeof(DateTimeOffset?)) return Expression.Constant(je.GetDateTimeOffset(), typeof(DateTimeOffset));
                        if (targetType == typeof(TimeSpan)) return Expression.Constant(je.GetDateTimeOffset().TimeOfDay, targetType);
                        if (targetType == typeof(bool)) return Expression.Constant(bool.Parse(je.GetString()!), targetType);
                        if (targetType == typeof(int)) return Expression.Constant(int.Parse(je.GetString()!), targetType);
                        if (targetType == typeof(long)) return Expression.Constant(long.Parse(je.GetString()!), targetType);
                        if (targetType == typeof(double)) return Expression.Constant(double.Parse(je.GetString()!), targetType);
                        
                        return targetType == typeof(short) ? 
                            Expression.Constant(short.Parse(je.GetString()!), targetType) 
                            : Expression.Constant(Convert.ChangeType(je.GetString(), targetType), targetType);

                    case JsonValueKind.True:
                        return Expression.Constant(true, targetType);
                    case JsonValueKind.False:
                        return Expression.Constant(false, targetType);
                    case JsonValueKind.Undefined:
                    case JsonValueKind.Object:
                    case JsonValueKind.Array:
                    case JsonValueKind.Null:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            }
        }

        if (value.GetType() == targetType || targetType.IsInstanceOfType(value))
            return Expression.Constant(value, targetType);
        
        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(Nullable<>))
            return Expression.Constant(Convert.ChangeType(value, targetType), targetType);
        
        var underlying = Nullable.GetUnderlyingType(targetType);
        
        if (underlying is not null && underlying.IsInstanceOfType(value))
            return Expression.Constant(value, targetType);
        return Expression.Constant(Convert.ChangeType(value, targetType), targetType);

    }
    
    public static List<(LambdaExpression KeySelector, bool Descending)> ExtractOrderings(Expression expression)
    {
        var orderings = new List<(LambdaExpression, bool)>();
        WalkOrderingChain(expression, orderings);
        orderings.Reverse();
        return orderings;

        static void WalkOrderingChain(Expression expression, List<(LambdaExpression KeySelector, bool Descending)> orderings)
        {
            while (true)
            {
                if (expression is not MethodCallExpression call) return;

                var method = call.Method;
                if (!method.IsGenericMethod) return;

                var def = method.GetGenericMethodDefinition();
                bool? descending = null;

                if (def == OrderByMethod || def == OrderByDescendingMethod)
                    descending = def == OrderByDescendingMethod;
                else if (def == ThenByMethod || def == ThenByDescendingMethod) descending = def == ThenByDescendingMethod;

                if (descending == null) return;

                if (call.Arguments is [_, UnaryExpression { Operand: LambdaExpression lambda }, ..])
                {
                    orderings.Add((lambda, descending.Value));
                }

                expression = call.Arguments[0];
            }
        }
    }

    private static Expression RebindParameter(LambdaExpression lambda, ParameterExpression target)
    {
        return new ParameterRebinder(target).Visit(lambda.Body);
    }

    private sealed class ParameterRebinder(ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node.Type == target.Type ? target : node;
    }
}