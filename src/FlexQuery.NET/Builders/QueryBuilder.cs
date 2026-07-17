using System.Linq.Expressions;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Security;
using FlexQuery.NET.Expressions;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Applies <see cref="QueryOptions"/> (filter, sort, page, select) to an
/// <see cref="IQueryable{T}"/> and materialises results.
/// </summary>
internal static class QueryBuilder
{
    /// <summary>Applies the filter group from <paramref name="options"/> to the query.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">The query options containing the filter to apply.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable<T> ApplyFilter<T>(IQueryable<T> query, QueryOptions options)
    {
        if (options.Filter is null || !HasAnyCondition(options.Filter)) return query;

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
                if (!SortBuilder.BuildAggregateExpression(parameter, sort, options, out keyExpression))
                    continue;
            }
            else
            {
                if (!SortBuilder.BuildPropertyExpression(parameter, sort.Field, options, out keyExpression))
                    continue;
            }

            var keyType = keyExpression.Type;
            var keySelector = Expression.Lambda(keyExpression, parameter);

            ordered = ordered is null
                ? SortBuilder.ApplyInitialOrder(query, keySelector, keyType, sort.Descending)
                : SortBuilder.ApplyThenOrder(ordered, keySelector, keyType, sort.Descending);
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
            var fieldName = options.Select?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Field) && !f.Field.Contains('.'));
            if (fieldName == null)
            {
                var allProps = ReflectionCache.GetProperties(typeof(T));
                var defaultSortProp = allProps
                    .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("Key", StringComparison.OrdinalIgnoreCase))
                    ?? allProps.FirstOrDefault();
                fieldName = defaultSortProp is null ? null : new SelectModel { Field = defaultSortProp.Name };
            }

            if (fieldName != null)
            {
                var defaultSortProp = ReflectionCache.GetProperty(typeof(T), fieldName.Field);
                if (defaultSortProp != null && !IsCollectionType(defaultSortProp.PropertyType))
                {
                    var parameter = Expression.Parameter(typeof(T), "x");
                    var property = Expression.Property(parameter, defaultSortProp);
                    var keySelector = Expression.Lambda(property, parameter);
                    query = SortBuilder.ApplyInitialOrder(query, keySelector, defaultSortProp.PropertyType, false);
                }
            }
        }

        return query.Skip(options.Paging.Skip).Take(options.Paging.PageSize);
    }

    /// <summary>Applies keyset (seek/cursor) pagination. Generates WHERE predicate from cursor values instead of Skip/Take.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable (must be sorted).</param>
    /// <param name="options">The query options containing sort metadata and cursor.</param>
    /// <returns>The queryable with keyset predicate applied.</returns>
    public static IQueryable<T> ApplyKeysetPaging<T>(IQueryable<T> query, QueryOptions options)
    {
        if (!options.IsKeysetMode)
            return ApplyPaging(query, options);

        if (options.Cursor is null)
            return query.Take(options.Paging.PageSize);
        
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<T>(options.Sort);
        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<T>(orderings, options.Cursor.Values);
        
        return query.Where(predicate).Take(options.Paging.PageSize);
        
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

        var tree = SelectTreeBuilder.Build(options);
        
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

        var projection = ProjectionBuilder.BuildExpression<T>(tree, options);
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
        => sort.Aggregate.HasValue;

    private static bool IsCollectionType(Type type)
        => SafePropertyResolver.TryGetCollectionElementType(type, out _);
    
    private static bool HasAnyCondition(FilterGroup group)
        => group.Filters.Count > 0 || group.Groups.Any(HasAnyCondition);
}
