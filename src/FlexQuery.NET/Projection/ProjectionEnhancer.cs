using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Projection;

/// <summary>
/// Injects filter-aligned operators (e.g. Where) into navigation projections.
/// </summary>
internal static class ProjectionEnhancer
{
    private static readonly MethodInfo QueryableWhere = ExpressionMethodCache.QueryableWhereSimple();

    /// <summary>
    /// If <paramref name="collectionFilter"/> can be translated into an element predicate,
    /// applies <c>Queryable.Where</c> to <paramref name="sourceQueryable"/> and returns the updated queryable.
    /// Returns <paramref name="sourceQueryable"/> unchanged when no filter can be built.
    /// </summary>
    public static Expression ApplyCollectionWhereIfNeeded(
        Expression sourceQueryable,
        Type elementType,
        FilterGroup? collectionFilter,
        QueryOptions options)
    {
        if (collectionFilter is null) return sourceQueryable;

        var predicate = BuildPredicateLambda(elementType, new QueryOptions
        {
            Filter = collectionFilter,
            CaseInsensitive = options.CaseInsensitive,
            EnableCache = options.EnableCache,
            UseEfCoreOperators = options.UseEfCoreOperators
        });
        if (predicate is null) return sourceQueryable;

        var whereMethod = QueryableWhere.MakeGenericMethod(elementType);

        return Expression.Call(null, whereMethod, sourceQueryable, predicate);
    }

    private static LambdaExpression? BuildPredicateLambda(Type elementType, QueryOptions options)
    {
        return ExpressionBuilder.BuildPredicate(elementType, options);
    }
}

