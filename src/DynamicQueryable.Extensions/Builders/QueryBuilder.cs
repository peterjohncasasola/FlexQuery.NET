using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Models;
using DynamicQueryable.Security;

namespace DynamicQueryable.Builders;

/// <summary>
/// Applies <see cref="QueryOptions"/> (filter, sort, page, select) to an
/// <see cref="IQueryable{T}"/> and materialises results.
/// </summary>
public static class QueryBuilder
{
    private static readonly MethodInfo OrderByMethod = GetQueryableMethod(nameof(Queryable.OrderBy));
    private static readonly MethodInfo OrderByDescendingMethod = GetQueryableMethod(nameof(Queryable.OrderByDescending));
    private static readonly MethodInfo ThenByMethod = GetQueryableMethod(nameof(Queryable.ThenBy));
    private static readonly MethodInfo ThenByDescendingMethod = GetQueryableMethod(nameof(Queryable.ThenByDescending));

    // ── Filter ───────────────────────────────────────────────────────────

    /// <summary>Applies the filter group from <paramref name="options"/> to the query.</summary>
    public static IQueryable<T> ApplyFilter<T>(IQueryable<T> query, QueryOptions options)
    {
        if (options.Filter is null) return query;
        if (!HasAnyCondition(options.Filter)) return query;

        var predicate = ExpressionBuilder.BuildPredicate<T>(options.Filter);
        return predicate is null ? query : query.Where(predicate);
    }

    // ── Sort ─────────────────────────────────────────────────────────────

    /// <summary>Applies ordered sorting from <paramref name="options"/> to the query.</summary>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, QueryOptions options)
        => ApplySort(query, options.Sort);

    /// <summary>Applies ordered sorting from <paramref name="sorts"/> to the query.</summary>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, List<SortOption>? sorts)
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
                if (!BuildAggregateExpression(parameter, sort, out keyExpression))
                    continue;
            }
            else
            {
                if (!BuildPropertyExpression(parameter, sort.Field, out keyExpression))
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

    // ── Paging ───────────────────────────────────────────────────────────

    /// <summary>Applies skip/take paging from <paramref name="options"/>.</summary>
    public static IQueryable<T> ApplyPaging<T>(IQueryable<T> query, QueryOptions options)
    {
        if (options?.Paging == null || options.Paging.Disabled) return query;
        return query.Skip(options.Paging.Skip).Take(options.Paging.PageSize);
    }

    // ── Select / Projection ──────────────────────────────────────────────

    /// <summary>
    /// Applies dynamic projection to the query.
    /// If Select is null or empty and no Includes are present, returns the original query cast to object.
    /// If Select or Includes have fields, builds a dynamic projection that includes only those fields.
    /// </summary>
    public static IQueryable<object> ApplySelect<T>(
        IQueryable<T> query, QueryOptions options)
    {
        if ((options.GroupBy?.Count ?? 0) > 0 || options.Aggregates.Count > 0)
        {
            return GroupByBuilder.Apply(query, options);
        }

        var tree = Helpers.SelectTreeBuilder.Build(options);
        
        if (!tree.HasChildren)
        {
            return query.Cast<object>();
        }

        var projection = ProjectionBuilder.Build<T>(tree, options.Filter);
        return query.Select(projection);
    }

    // ── All-in-one ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies filter → sort → paging sequentially and returns the paged queryable.
    /// Does NOT apply projection. Use <see cref="ApplySelect{T}"/> on the result to project.
    /// </summary>
    public static IQueryable<T> Apply<T>(IQueryable<T> query, QueryOptions options)
    {
        query = ApplyFilter(query, options);
        query = ApplySort(query, options);
        query = ApplyPaging(query, options);
        return query;
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static bool HasAggregate(SortOption sort)
        => !string.IsNullOrWhiteSpace(sort.Aggregate);

    private static bool BuildPropertyExpression(
        Expression parameter,
        string path,
        out Expression propertyExpression)
    {
        propertyExpression = null!;
        if (!SafePropertyResolver.TryResolveChain(parameter.Type, path, out var chain))
            return false;
        if (chain.Count == 0)
            return false;

        // Sorting on collection navigations is not supported.
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
        SortOption sort,
        out Expression aggregateExpression)
    {
        aggregateExpression = null!;

        if (string.IsNullOrWhiteSpace(sort.Aggregate)
            || !SafePropertyResolver.TryResolveChain(parameter.Type, sort.Field, out var chain)
            || chain.Count == 0)
        {
            return false;
        }

        var collectionProp = chain[^1];
        if (!IsCollectionType(collectionProp.PropertyType))
            return false;

        // Aggregate sorting only supports a collection at the final segment.
        if (chain.Take(chain.Count - 1).Any(p => IsCollectionType(p.PropertyType)))
            return false;

        Expression collectionAccess = parameter;
        foreach (var prop in chain)
        {
            collectionAccess = Expression.Property(collectionAccess, prop);
        }

        if (!SafePropertyResolver.TryGetCollectionElementType(collectionProp.PropertyType, out var elementType))
            return false;

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
        var countMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(Enumerable.Count)
                         && m.GetParameters().Length == 1)
            .MakeGenericMethod(elementType);

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

        // SQLite cannot translate decimal collection aggregates directly.
        // Promote decimal aggregates to double while keeping the query server-translatable.
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

        MethodInfo method;

        if (aggregate is "max" or "min")
        {
            method = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name.Equals(aggregate, StringComparison.OrdinalIgnoreCase)
                             && m.IsGenericMethodDefinition
                             && m.GetGenericArguments().Length == 2
                             && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType, effectiveSelectorType);
        }
        else if (aggregate is "sum" or "avg")
        {
            var selectedMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.Equals(aggregate, StringComparison.OrdinalIgnoreCase)
                            && m.IsGenericMethodDefinition
                            && m.GetGenericArguments().Length == 1
                            && m.GetParameters().Length == 2)
                .FirstOrDefault(m =>
                {
                    var selectorParamType = m.GetParameters()[1].ParameterType;
                    if (!selectorParamType.IsGenericType) return false;
                    var args = selectorParamType.GetGenericArguments();
                    return args.Length == 2 && args[1] == effectiveSelectorType;
                });

            if (selectedMethod is null) return null;
            method = selectedMethod.MakeGenericMethod(elementType);
        }
        else
        {
            return null;
        }

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

    private static MethodInfo GetQueryableMethod(string name)
        => typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m =>
                m.Name == name
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2);

    private static bool HasAnyCondition(FilterGroup group)
        => group.Filters.Count > 0 || group.Groups.Any(g => HasAnyCondition(g));
}
