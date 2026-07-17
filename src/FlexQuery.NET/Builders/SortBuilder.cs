using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Builders;

internal static class SortBuilder
{
    private static readonly MethodInfo OrderByMethod = ExpressionMethodCache.QueryableOrderBy();
    private static readonly MethodInfo OrderByDescendingMethod = ExpressionMethodCache.QueryableOrderByDescending();
    private static readonly MethodInfo ThenByMethod = ExpressionMethodCache.QueryableThenBy();
    private static readonly MethodInfo ThenByDescendingMethod = ExpressionMethodCache.QueryableThenByDescending();
    
    public static IOrderedQueryable<T> ApplyInitialOrder<T>(
        IQueryable<T> query,
        LambdaExpression keySelector,
        Type keyType,
        bool descending)
    {
        var method = (descending ? OrderByDescendingMethod : OrderByMethod)
            .MakeGenericMethod(typeof(T), keyType);

        var orderedQuery = method.Invoke(null, [query, keySelector]);
        return (IOrderedQueryable<T>)orderedQuery!;
    }

    public static IOrderedQueryable<T> ApplyThenOrder<T>(
        IOrderedQueryable<T> query,
        LambdaExpression keySelector,
        Type keyType,
        bool descending)
    {
        var method = (descending ? ThenByDescendingMethod : ThenByMethod)
            .MakeGenericMethod(typeof(T), keyType);

        var orderedQuery = method.Invoke(null, [query, keySelector]);
        return (IOrderedQueryable<T>)orderedQuery!;
    }
    
    public static bool BuildAggregateExpression(
        Expression parameter,
        SortNode sort,
        QueryOptions options,
        out Expression aggregateExpression)
    {
        aggregateExpression = null!;
        Expression collectionAccess;
        Type elementType;

        if (FieldResolver.TryResolveMappedExpression(parameter, sort.Field, options, out var resolvedExpr, out var resolvedType))
        {
            if (!IsCollectionType(resolvedType)) return false;
            if (!SafePropertyResolver.TryGetCollectionElementType(resolvedType, out elementType)) return false;
            collectionAccess = resolvedExpr;
        }
        else
        {
            if (!SafePropertyResolver.TryResolveChain(parameter.Type, sort.Field, out var chain)
                || chain.Count == 0)
            {
                return false;
            }

            var collectionProp = chain[^1];
            if (!IsCollectionType(collectionProp.PropertyType))
                return false;

            if (chain.Take(chain.Count - 1).Any(p => IsCollectionType(p.PropertyType)))
                return false;

            collectionAccess = parameter;
            foreach (var prop in chain)
            {
                collectionAccess = Expression.Property(collectionAccess, prop);
            }

            if (!SafePropertyResolver.TryGetCollectionElementType(collectionProp.PropertyType, out elementType))
                return false;
        }

        var aggregate = sort.Aggregate!.Value;
        if (aggregate == AggregateFunction.Count)
        {
            if (!string.IsNullOrWhiteSpace(sort.AggregateField))
                return false;

            aggregateExpression = BuildCountExpression(collectionAccess, elementType);
            return true;
        }

        if (string.IsNullOrWhiteSpace(sort.AggregateField))
            return false;

        if (!BuildElementSelectorExpression(elementType, sort.AggregateField!, out var selectorLambda, out var selectorType))
            return false;

        if (selectorType == typeof(string))
            return false;

        var builtAggregate = BuildSelectorAggregateExpression(
            aggregate,
            collectionAccess,
            elementType,
            selectorLambda,
            selectorType);

        if (builtAggregate is null)
            return false;

        aggregateExpression = builtAggregate;
        return true;
    }
    
    public static bool BuildPropertyExpression(
        Expression parameter,
        string path,
        QueryOptions options,
        out Expression propertyExpression)
    {
        propertyExpression = null!;

        if (FieldResolver.TryResolveMappedExpression(parameter, path, options, out var resolvedExpr, out var resolvedType))
        {
            if (IsCollectionType(resolvedType)) return false;
            propertyExpression = resolvedExpr;
            return true;
        }

        if (!SafePropertyResolver.TryResolveChain(parameter.Type, path, out var chain))
            return false;
        if (chain.Count == 0)
            return false;

        if (chain.Any(p => IsCollectionType(p.PropertyType)))
            return false;

        var access = parameter;
        foreach (var prop in chain)
        {
            access = Expression.Property(access, prop);
        }

        propertyExpression = access;
        return true;
    }

    private static bool BuildElementSelectorExpression(
        Type elementType,
        string path,
        out LambdaExpression selectorLambda,
        out Type selectorType)
    {
        selectorLambda = null!;
        selectorType = null!;

        if (path.Contains('.', StringComparison.Ordinal))
            return false;

        if (!SafePropertyResolver.TryResolveChain(elementType, path, out var valueChain) || valueChain.Count == 0)
            return false;
        if (valueChain.Any(p => IsCollectionType(p.PropertyType)))
            return false;

        var item = Expression.Parameter(elementType, "e");
        var body = valueChain.Aggregate<PropertyInfo?, Expression>(item, Expression.Property!);

        selectorType = body.Type;
        selectorLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(elementType, selectorType),
            body,
            item);

        return true;
    }
    
    private static Expression BuildCountExpression(Expression collectionAccess, Type elementType)
    {
        var countMethod = ExpressionMethodCache.EnumerableCount(elementType);
        return Expression.Call(countMethod, collectionAccess);
    }
    
    private static Expression? BuildSelectorAggregateExpression(
        AggregateFunction aggregate,
        Expression collectionAccess,
        Type elementType,
        LambdaExpression selectorLambda,
        Type selectorType)
    {
        var effectiveSelectorType = selectorType;
        var effectiveSelectorLambda = selectorLambda;

        if (selectorType == typeof(decimal))
        {
            effectiveSelectorType = typeof(double);
            effectiveSelectorLambda = ConvertSelectorLambda(selectorLambda, effectiveSelectorType);
        }
        else if (selectorType == typeof(decimal?))
        {
            effectiveSelectorType = typeof(double?);
            effectiveSelectorLambda = ConvertSelectorLambda(selectorLambda, effectiveSelectorType);
        }

        var method = aggregate switch
        {
            AggregateFunction.Max => ExpressionMethodCache.EnumerableMaxWithSelector(elementType, effectiveSelectorType),
            AggregateFunction.Min => ExpressionMethodCache.EnumerableMinWithSelector(elementType, effectiveSelectorType),
            AggregateFunction.Sum => ExpressionMethodCache.EnumerableSumWithSelector(elementType, effectiveSelectorType),
            AggregateFunction.Avg => ExpressionMethodCache.EnumerableAverageWithSelector(elementType, effectiveSelectorType),
            _ => null!
        };

        return Expression.Call(method, collectionAccess, effectiveSelectorLambda);
    }
    
    private static LambdaExpression ConvertSelectorLambda(
        LambdaExpression selectorLambda,
        Type targetSelectorType)
    {
        var parameter = selectorLambda.Parameters[0];
        var convertedBody = Expression.Convert(selectorLambda.Body, targetSelectorType);
        return Expression.Lambda(
            typeof(Func<,>).MakeGenericType(parameter.Type, targetSelectorType),
            convertedBody,
            parameter);
    }
    
    private static bool IsCollectionType(Type type)
        => SafePropertyResolver.TryGetCollectionElementType(type, out _);
}