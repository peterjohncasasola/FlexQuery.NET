using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Projection;

/// <summary>
/// Injects expand-aligned operators into navigation projections.
/// </summary>
internal static class ProjectionEnhancer
{
    private static readonly MethodInfo QueryableWhere = ExpressionMethodCache.QueryableWhereSimple();
    private static readonly MethodInfo QueryableOrderBy = ExpressionMethodCache.QueryableOrderBy();
    private static readonly MethodInfo QueryableOrderByDescending = ExpressionMethodCache.QueryableOrderByDescending();
    private static readonly MethodInfo QueryableThenBy = ExpressionMethodCache.QueryableThenBy();
    private static readonly MethodInfo QueryableThenByDescending = ExpressionMethodCache.QueryableThenByDescending();
    private static readonly MethodInfo QueryableTake = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == nameof(Queryable.Take)
            && m.IsGenericMethodDefinition
            && m.GetParameters().Length == 2);

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
        => ApplyCollectionOperatorsIfNeeded(sourceQueryable, elementType, collectionFilter, null, null, options);

    /// <summary>
    /// Applies collection-level expand operators before the projection <c>Select</c> so EF can translate them to SQL.
    /// </summary>
    public static Expression ApplyCollectionOperatorsIfNeeded(
        Expression sourceQueryable,
        Type elementType,
        FilterGroup? collectionFilter,
        IReadOnlyList<SortNode>? sort,
        int? take,
        QueryOptions options)
    {
        var result = sourceQueryable;

        if (collectionFilter is not null)
        {
            var predicate = BuildPredicateLambda(elementType, new QueryOptions
            {
                Filter = collectionFilter,
                EnableCache = options.EnableCache,
                UseEfCoreOperators = options.UseEfCoreOperators
            });

            if (predicate is not null)
            {
                var whereMethod = QueryableWhere.MakeGenericMethod(elementType);
                result = Expression.Call(null, whereMethod, result, predicate);
            }
        }

        result = ApplySort(result, elementType, sort, options);

        if (take is > 0)
        {
            var takeMethod = QueryableTake.MakeGenericMethod(elementType);
            result = Expression.Call(null, takeMethod, result, Expression.Constant(take.Value));
        }

        return result;
    }

    private static LambdaExpression? BuildPredicateLambda(Type elementType, QueryOptions options)
    {
        return ExpressionBuilder.BuildPredicate(elementType, options);
    }

    private static Expression ApplySort(
        Expression sourceQueryable,
        Type elementType,
        IReadOnlyList<SortNode>? sort,
        QueryOptions options)
    {
        if (sort is not { Count: > 0 })
            return sourceQueryable;

        Expression result = sourceQueryable;
        var ordered = false;

        foreach (var sortNode in sort)
        {
            var parameter = Expression.Parameter(elementType, "e");
            if (!SortBuilder.BuildPropertyExpression(parameter, sortNode.Field, options, out var keyExpression))
                continue;

            var selectorType = typeof(Func<,>).MakeGenericType(elementType, keyExpression.Type);
            var selector = Expression.Lambda(selectorType, keyExpression, parameter);
            var method = (ordered, sortNode.Descending) switch
            {
                (false, false) => QueryableOrderBy,
                (false, true) => QueryableOrderByDescending,
                (true, false) => QueryableThenBy,
                (true, true) => QueryableThenByDescending
            };

            result = Expression.Call(
                null,
                method.MakeGenericMethod(elementType, keyExpression.Type),
                result,
                selector);

            ordered = true;
        }

        return result;
    }
}

