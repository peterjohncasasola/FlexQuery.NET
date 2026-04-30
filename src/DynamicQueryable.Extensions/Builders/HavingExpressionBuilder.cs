using System.Linq.Expressions;
using DynamicQueryable.Models;
using DynamicQueryable.Security;

namespace DynamicQueryable.Builders;

internal static class HavingExpressionBuilder
{
    public static LambdaExpression? Build(Type projectionType, HavingCondition? having, string aggregateAlias)
    {
        if (having is null || string.IsNullOrWhiteSpace(aggregateAlias))
            return null;

        var parameter = Expression.Parameter(projectionType, "g");
        var member = projectionType.GetProperty(aggregateAlias);
        if (member is null) return null;

        var memberExpr = Expression.Property(parameter, member);
        var predicate = SafeConditionBuilder.Build(memberExpr, having.Operator, having.Value);
        if (predicate is null) return null;

        var delegateType = typeof(Func<,>).MakeGenericType(projectionType, typeof(bool));
        return Expression.Lambda(delegateType, predicate, parameter);
    }
}
