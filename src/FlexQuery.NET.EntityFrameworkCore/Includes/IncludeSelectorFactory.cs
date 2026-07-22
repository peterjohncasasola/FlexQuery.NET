using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Builds the selector lambdas passed to <c>Include</c> / <c>ThenInclude</c>,
/// optionally embedding filtered-include collection operators.
/// </summary>
internal static class IncludeSelectorFactory
{
    /// <summary>
    /// Builds <c>parent => parent.Nav</c>, or - for a collection navigation
    /// where <paramref name="allowFilteredCollection"/> is <c>true</c> -
    /// embeds expand operators such as <c>Where</c>, <c>OrderBy</c>, and <c>Take</c>.
    /// </summary>
    public static LambdaExpression Build(
        Type parentType,
        NavigationInfo navigation,
        IncludeNode node,
        QueryOptions options,
        bool allowFilteredCollection)
    {
        var parentParam = Expression.Parameter(parentType, "x");
        Expression body = Expression.Property(parentParam, navigation.Property);

        if (navigation.IsCollection && allowFilteredCollection)
        {
            if (node.Filter is not null)
            {
                var predicate = ExpressionBuilder.BuildPredicate(navigation.TargetType, new QueryOptions
                {
                    Filter = node.Filter,
                    CaseInsensitive = options.CaseInsensitive,
                    EnableCache = options.EnableCache,
                    UseEfCoreOperators = options.UseEfCoreOperators
                });

                if (predicate is not null)
                    body = BuildWhereCall(body, navigation.TargetType, predicate);
            }

            body = ApplySort(body, navigation.TargetType, node.Sort, options);
            body = ApplyTake(body, navigation.TargetType, node.Take);
        }

        var propertyType = navigation.GetPropertyTypeForSelector(allowFilteredCollection);
        var selectorType = typeof(Func<,>).MakeGenericType(parentType, propertyType);
        return Expression.Lambda(selectorType, body, parentParam);
    }

    /// <summary>
    /// Builds <c>collection.Where(predicate)</c> as an expression node so it
    /// can be embedded inside a selector lambda (EF Core's filtered-include pattern).
    /// </summary>
    private static MethodCallExpression BuildWhereCall(Expression collectionAccess, Type elementType, LambdaExpression predicate)
    {
        var whereMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Enumerable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(whereMethod, collectionAccess, predicate);
    }

    private static Expression ApplySort(
        Expression collectionAccess,
        Type elementType,
        IReadOnlyList<SortNode>? sort,
        QueryOptions options)
    {
        if (sort is not { Count: > 0 })
            return collectionAccess;

        Expression result = collectionAccess;
        var ordered = false;

        foreach (var sortNode in sort)
        {
            var parameter = Expression.Parameter(elementType, "e");
            if (!SortBuilder.BuildPropertyExpression(parameter, sortNode.Field, options, out var keyExpression))
                continue;

            var selectorType = typeof(Func<,>).MakeGenericType(elementType, keyExpression.Type);
            var selector = Expression.Lambda(selectorType, keyExpression, parameter);
            var methodName = (ordered, sortNode.Descending) switch
            {
                (false, false) => nameof(Enumerable.OrderBy),
                (false, true) => nameof(Enumerable.OrderByDescending),
                (true, false) => nameof(Enumerable.ThenBy),
                (true, true) => nameof(Enumerable.ThenByDescending)
            };

            var orderMethod = FindEnumerableOrderingMethod(methodName).MakeGenericMethod(elementType, keyExpression.Type);
            result = Expression.Call(orderMethod, result, selector);
            ordered = true;
        }

        return result;
    }

    private static Expression ApplyTake(Expression collectionAccess, Type elementType, int? take)
    {
        if (take is not > 0)
            return collectionAccess;

        var takeMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Enumerable.Take)
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(takeMethod, collectionAccess, Expression.Constant(take.Value));
    }

    private static MethodInfo FindEnumerableOrderingMethod(string methodName)
    {
        return typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == methodName
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 2);
    }
}
