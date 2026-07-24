using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Expressions;

/// <summary>
/// Builds a LINQ predicate from a <see cref="HavingNode"/> tree.
/// </summary>
internal static class HavingExpressionBuilder
{
    public static LambdaExpression? Build(Type projectionType, HavingNode? having, List<Aggregate> aggregates)
    {
        if (having is null) return null;

        var parameter = Expression.Parameter(projectionType, "g");
        var body = BuildExpression(having, parameter, projectionType, aggregates);
        if (body is null) return null;

        var delegateType = typeof(Func<,>).MakeGenericType(projectionType, typeof(bool));
        return Expression.Lambda(delegateType, body, parameter);
    }

    private static Expression? BuildExpression(HavingNode node, ParameterExpression parameter, Type projectionType, List<Aggregate> aggregates)
    {
        switch (node)
        {
            case HavingConditionNode c:
                return BuildCondition(c, parameter, projectionType, aggregates);
            case HavingLogicalNode l:
            {
                var parts = new List<Expression>();
                foreach (var child in l.Children)
                {
                    var childExpr = BuildExpression(child, parameter, projectionType, aggregates);
                    if (childExpr is not null) parts.Add(childExpr);
                }

                switch (parts.Count)
                {
                    case 0:
                        return null;
                    case 1:
                        return parts[0];
                    default:
                    {
                        var logic = l.Logic.ToKeyword();
                        var result = parts.Aggregate((left, right) =>
                            logic.Equals("or", StringComparison.OrdinalIgnoreCase)
                                ? Expression.OrElse(left, right)
                                : Expression.AndAlso(left, right));

                        return result;
                    }
                }
            }
            case HavingGroupNode g:
                return BuildExpression(g.Inner, parameter, projectionType, aggregates);
            default:
                return null;
        }
    }

    private static Expression? BuildCondition(HavingConditionNode condition, ParameterExpression parameter, Type projectionType, List<Aggregate> aggregates)
    {
        var aggregateAlias = ResolveAggregateAlias(condition, aggregates);
        if (aggregateAlias is null) return null;

        var member = projectionType.GetProperty(aggregateAlias);
        if (member is null) return null;

        var memberExpr = Expression.Property(parameter, member);
        var predicate = FilterExpressionBuilder.Build(memberExpr, condition.Operator, condition.Value);
        return predicate;
    }

    private static string? ResolveAggregateAlias(HavingConditionNode condition, List<Aggregate> aggregates)
    {
        var matching = aggregates.FirstOrDefault(a =>
            a.Function == condition.Function &&
            string.Equals(a.Field, condition.Field, StringComparison.OrdinalIgnoreCase));

        return matching?.Alias;
    }
}
