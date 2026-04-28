using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Exceptions;
using DynamicQueryable.Models;

namespace DynamicQueryable.Builders;

/// <summary>
/// Applies <see cref="QueryOptions"/> (filter, sort, page, select) to an
/// <see cref="IQueryable{T}"/> and materialises results.
/// </summary>
public static class QueryBuilder
{
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

            var keySelector = BuildKeySelector<T>(sort.Field)
                ?? throw new InvalidSortFieldException(sort.Field, typeof(T));

            ordered = ordered is null
                ? (sort.Descending
                    ? query.OrderByDescending(keySelector)
                    : query.OrderBy(keySelector))
                : (sort.Descending
                    ? ordered.ThenByDescending(keySelector)
                    : ordered.ThenBy(keySelector));
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

        var projection = ProjectionBuilder.Build<T>(tree);
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

    private static Expression<Func<T, object>>? BuildKeySelector<T>(string field)
    {
        var type = typeof(T);
        var prop = type.GetProperty(field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null) return null;

        var param      = Expression.Parameter(type, "x");
        var propAccess = Expression.Property(param, prop);
        var boxed      = Expression.Convert(propAccess, typeof(object));
        return Expression.Lambda<Func<T, object>>(boxed, param);
    }

    private static bool HasAnyCondition(FilterGroup group)
        => group.Filters.Count > 0 || group.Groups.Any(g => HasAnyCondition(g));
}
