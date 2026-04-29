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
    {
        if (options.Sort is null || options.Sort.Count == 0) return query;

        IOrderedQueryable<T>? ordered = null;

        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            if (!TryBuildKeySelectorLambda<T>(sort.Field, out var keySelector, out var keyType))
                continue;

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

    private static bool TryBuildKeySelectorLambda<T>(string field, out LambdaExpression keySelector, out Type keyType)
    {
        keySelector = null!;
        keyType = null!;

        if (!SafePropertyResolver.TryResolveChain(typeof(T), field, out var chain))
            return false;
        if (chain.Count == 0)
            return false;

        // Sorting on collection navigations is not supported.
        if (chain.Any(p => SafePropertyResolver.TryGetCollectionElementType(p.PropertyType, out _)))
            return false;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression access = parameter;
        foreach (var prop in chain)
        {
            access = Expression.Property(access, prop);
        }

        keyType = access.Type;
        keySelector = Expression.Lambda(access, parameter);
        return true;
    }

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
