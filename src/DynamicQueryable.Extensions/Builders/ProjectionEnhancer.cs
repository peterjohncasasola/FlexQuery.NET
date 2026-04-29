using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Models;

namespace DynamicQueryable.Builders;

/// <summary>
/// Injects filter-aligned operators (e.g. Where) into navigation projections.
/// </summary>
internal static class ProjectionEnhancer
{
    private static readonly MethodInfo _queryableWhere2 =
        typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where)
                        && m.GetParameters().Length == 2);

    /// <summary>
    /// If <paramref name="collectionFilter"/> can be translated into an element predicate,
    /// applies <c>Queryable.Where</c> to <paramref name="sourceQueryable"/> and returns the updated queryable.
    /// Returns <paramref name="sourceQueryable"/> unchanged when no filter can be built.
    /// </summary>
    public static Expression ApplyCollectionWhereIfNeeded(
        Expression sourceQueryable,
        Type elementType,
        FilterGroup? collectionFilter)
    {
        if (collectionFilter is null) return sourceQueryable;

        var predicate = BuildPredicateLambda(elementType, collectionFilter);
        if (predicate is null) return sourceQueryable;

        var whereMethod = _queryableWhere2.MakeGenericMethod(elementType);

        return Expression.Call(null, whereMethod, sourceQueryable, predicate);
    }

    private static LambdaExpression? BuildPredicateLambda(Type elementType, FilterGroup filter)
    {
        // Use the existing ExpressionBuilder without altering the parent filter logic.
        var method = typeof(ExpressionBuilder).GetMethod(nameof(ExpressionBuilder.BuildPredicate), BindingFlags.Public | BindingFlags.Static);
        if (method is null) return null;

        var generic = method.MakeGenericMethod(elementType);
        var result = generic.Invoke(null, [filter]);

        return result as LambdaExpression;
    }
}

