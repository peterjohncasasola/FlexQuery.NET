using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Projection;
using FlexQuery.NET.Security;
using FlexQuery.NET.Expressions;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Applies <see cref="QueryOptions"/> (filter, sort, page, select) to an
/// <see cref="IQueryable{T}"/> and materialises results.
/// </summary>
public static class QueryBuilder
{
    private static readonly MethodInfo OrderByMethod = ExpressionMethodCache.QueryableOrderBy();
    private static readonly MethodInfo OrderByDescendingMethod = ExpressionMethodCache.QueryableOrderByDescending();
    private static readonly MethodInfo ThenByMethod = ExpressionMethodCache.QueryableThenBy();
    private static readonly MethodInfo ThenByDescendingMethod = ExpressionMethodCache.QueryableThenByDescending();

    /// <summary>Applies the filter group from <paramref name="options"/> to the query.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing the filter to apply.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable<T> ApplyFilter<T>(IQueryable<T> query, QueryOptions options)
    {
        if (options.Filter is null) return query;
        if (!HasAnyCondition(options.Filter)) return query;

        var predicate = ExpressionBuilder.BuildPredicate<T>(options);
        return predicate is null ? query : query.Where(predicate);
    }

    /// <summary>Applies ordered sorting from <paramref name="options"/> to the query.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing the sort instructions.</param>
    /// <returns>The sorted queryable.</returns>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, QueryOptions options)
        => ApplySort(query, options.Sort, options);

    /// <summary>Applies ordered sorting from <paramref name="sorts"/> to the query.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="sorts">The list of sort nodes to apply.</param>
    /// <param name="options">The query options containing expression mappings.</param>
    /// <returns>The sorted queryable.</returns>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, List<SortNode>? sorts, QueryOptions options)
    {
        if (sorts is null || sorts.Count == 0) return query;

        IOrderedQueryable<T>? ordered = null;

        foreach (var sort in sorts)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression keyExpression;
            if (HasAggregate(sort))
            {
                if (!BuildAggregateExpression(parameter, sort, options, out keyExpression))
                    continue;
            }
            else
            {
                if (!BuildPropertyExpression(parameter, sort.Field, options, out keyExpression))
                    continue;
            }

            var keyType = keyExpression.Type;
            var keySelector = Expression.Lambda(keyExpression, parameter);

            ordered = ordered is null
                ? ApplyInitialOrder(query, keySelector, keyType, sort.Descending)
                : ApplyThenOrder(ordered, keySelector, keyType, sort.Descending);
        }

        return ordered ?? query;
    }

    /// <summary>Applies skip/take paging from <paramref name="options"/>.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing paging parameters.</param>
    /// <returns>The paged queryable.</returns>
    public static IQueryable<T> ApplyPaging<T>(IQueryable<T> query, QueryOptions options)
    {
        if (options?.Paging == null || options.Paging.Disabled) return query;

        if (options.Paging.Skip > 0 && query is not IOrderedQueryable<T>)
        {
            string? fieldName = options.Select?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && !f.Contains('.'));
            if (fieldName == null)
            {
                var allProps = ReflectionCache.GetProperties(typeof(T));
                var defaultSortProp = allProps
                    .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("Key", StringComparison.OrdinalIgnoreCase))
                    ?? allProps.FirstOrDefault();
                fieldName = defaultSortProp?.Name;
            }

            if (fieldName != null)
            {
                var defaultSortProp = ReflectionCache.GetProperty(typeof(T), fieldName);
                if (defaultSortProp != null && !IsCollectionType(defaultSortProp.PropertyType))
                {
                    var parameter = Expression.Parameter(typeof(T), "x");
                    var property = Expression.Property(parameter, defaultSortProp);
                    var keySelector = Expression.Lambda(property, parameter);
                    query = ApplyInitialOrder(query, keySelector, defaultSortProp.PropertyType, false);
                }
            }
        }

        return query.Skip(options.Paging.Skip).Take(options.Paging.PageSize);
    }

    /// <summary>
    /// Applies dynamic projection to the query.
    /// If Select is null or empty and no Includes are present, returns the original query cast to object.
    /// If Select or Includes have fields, builds a dynamic projection that includes only those fields.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing projection settings.</param>
    /// <returns>A queryable of projected objects.</returns>
    public static IQueryable<object> ApplySelect<T>(
        IQueryable<T> query, QueryOptions options)
    {
        if ((options.GroupBy?.Count ?? 0) > 0)
        {
            return GroupByBuilder.Apply(query, options);
        }

        var tree = Helpers.SelectTreeBuilder.Build(options);
        
        if (!tree.HasChildren)
        {
            return query.Cast<object>();
        }

        if (options.ProjectionMode == ProjectionMode.Flat)
        {
            return FlatProjectionBuilder.BuildAndApply(query, tree, options);
        }

        if (options.ProjectionMode == ProjectionMode.FlatMixed)
        {
            return FlatProjectionBuilder.BuildAndApplyMixed(query, tree, options);
        }

        var projection = ProjectionExpressionBuilder.BuildExpression<T>(tree, options);
        return query.Select(projection);
    }

    /// <summary>
    /// Applies filter, sort, and paging sequentially and returns the paged queryable.
    /// Does NOT apply projection. Use <see cref="ApplySelect{T}"/> on the result to project.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options to apply.</param>
    /// <returns>The filtered, sorted, and paged queryable.</returns>
    public static IQueryable<T> Apply<T>(IQueryable<T> query, QueryOptions options)
    {
        query = ApplyFilter(query, options);
        query = ApplySort(query, options);
        query = ApplyPaging(query, options);
        return query;
    }

    private static bool HasAggregate(SortNode sort)
        => !string.IsNullOrWhiteSpace(sort.Aggregate);

    private static bool BuildPropertyExpression(
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

        Expression access = parameter;
        foreach (var prop in chain)
        {
            access = Expression.Property(access, prop);
        }

        propertyExpression = access;
        return true;
    }

    private static bool BuildAggregateExpression(
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
            if (string.IsNullOrWhiteSpace(sort.Aggregate)
                || !SafePropertyResolver.TryResolveChain(parameter.Type, sort.Field, out var chain)
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

        var aggregate = sort.Aggregate!.Trim().ToLowerInvariant();
        if (aggregate == "count")
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

    private static Expression BuildCountExpression(Expression collectionAccess, Type elementType)
    {
        var countMethod = ExpressionMethodCache.EnumerableCount(elementType);
        return Expression.Call(countMethod, collectionAccess);
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
        Expression body = item;
        foreach (var prop in valueChain)
        {
            body = Expression.Property(body, prop);
        }

        selectorType = body.Type;
        selectorLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(elementType, selectorType),
            body,
            item);

        return true;
    }

    private static Expression? BuildSelectorAggregateExpression(
        string aggregate,
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
            "max" => ExpressionMethodCache.EnumerableMaxWithSelector(elementType, effectiveSelectorType),
            "min" => ExpressionMethodCache.EnumerableMinWithSelector(elementType, effectiveSelectorType),
            "sum" => ExpressionMethodCache.EnumerableSumWithSelector(elementType, effectiveSelectorType),
            "avg" => ExpressionMethodCache.EnumerableAverageWithSelector(elementType, effectiveSelectorType),
            "average" => ExpressionMethodCache.EnumerableAverageWithSelector(elementType, effectiveSelectorType),
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

    private static IOrderedQueryable<T> ApplyInitialOrder<T>(
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

    private static IOrderedQueryable<T> ApplyThenOrder<T>(
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
    
    private static bool HasAnyCondition(FilterGroup group)
        => group.Filters.Count > 0 || group.Groups.Any(g => HasAnyCondition(g));
}

