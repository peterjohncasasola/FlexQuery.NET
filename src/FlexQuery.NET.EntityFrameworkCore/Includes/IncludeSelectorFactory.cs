using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Builds the selector lambdas passed to <c>Include</c> / <c>ThenInclude</c>,
/// optionally embedding a <c>.Where(...)</c> filter on collection navigations.
/// </summary>
internal static class IncludeSelectorFactory
{
    /// <summary>
    /// Builds <c>parent => parent.Nav</c>, or — for a collection navigation
    /// where <paramref name="allowFilteredCollection"/> is <c>true</c> and a
    /// <paramref name="filter"/> is supplied — <c>parent => parent.Nav.Where(predicate)</c>.
    /// </summary>
    public static LambdaExpression Build(
        Type parentType,
        NavigationInfo navigation,
        FilterGroup? filter,
        QueryOptions options,
        bool allowFilteredCollection)
    {
        var parentParam = Expression.Parameter(parentType, "x");
        Expression body = Expression.Property(parentParam, navigation.Property);

        if (navigation.IsCollection && allowFilteredCollection && filter is not null)
        {
            var predicate = ExpressionBuilder.BuildPredicate(navigation.TargetType, new QueryOptions
            {
                Filter = filter,
                CaseInsensitive = options.CaseInsensitive,
                EnableCache = options.EnableCache,
                UseEfCoreOperators = options.UseEfCoreOperators
            });

            if (predicate is not null)
                body = BuildWhereCall(body, navigation.TargetType, predicate);
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
}