using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EFCore;

/// <summary>
/// EF Core-specific async extensions for materializing query results.
/// </summary>
public static class QueryableEfCoreExtensions
{
    /// <summary>
    /// Async variant of <c>ToQueryResult</c> for EF Core query providers.
    /// </summary>
    public static async Task<QueryResult<T>> ToQueryResultAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        var filtered = QueryBuilder.ApplyFilter(query, options);
        filtered = QueryBuilder.ApplySort(filtered, options);

        var total = await filtered.CountAsync(cancellationToken);

        var paged = QueryBuilder.ApplyPaging(filtered, options);
        var data = await paged.ApplyFilteredIncludes(options).ToListAsync(cancellationToken);

        return new QueryResult<T>
        {
            TotalCount = total,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Data = data
        };
    }

    /// <summary>
    /// Async projected result variant using options-driven selection.
    /// </summary>
    public static async Task<QueryResult<object>> ToProjectedQueryResultAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        var filtered = QueryBuilder.ApplyFilter(query, options);
        filtered = QueryBuilder.ApplySort(filtered, options);

        var total = await filtered.CountAsync(cancellationToken);

        var paged = QueryBuilder.ApplyPaging(filtered, options);
        // Note: ApplySelect already incorporates FilteredIncludes filters into the projection tree,
        // so calling ApplyFilteredIncludes here is technically redundant but ensures consistency
        // if the projection engine behavior changes.
        var data = await paged.ApplyFilteredIncludes(options).ApplySelect(options).ToListAsync(cancellationToken);

        return new QueryResult<object>
        {
            TotalCount = total,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
            Data = data
        };
    }

    // ── Include Pipeline ─────────────────────────────────────────────────

    /// <summary>
    /// Applies the <b>Include Pipeline</b>: translates every
    /// <see cref="QueryOptions.FilteredIncludes"/>
    /// into EF Core <c>Include</c> / <c>ThenInclude</c> calls, each optionally
    /// filtered by an inline <c>Where</c> clause.
    ///
    /// <para>
    /// This pipeline is <b>completely independent</b> of the WHERE pipeline
    /// (<see cref="QueryableEfCoreExtensions.ToQueryResultAsync{T}"/>).
    /// It must be called <em>before</em> any materialisation (e.g.
    /// <c>ToListAsync</c>) but after <c>ApplyQueryOptions</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var options = QueryOptionsParser.Parse(Request.Query);
    ///
    /// var result = await _context.Customers
    ///     .ApplyQueryOptions(options)           // WHERE pipeline
    ///     .ApplyFilteredIncludes(options)       // INCLUDE pipeline
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public static IQueryable<T> ApplyFilteredIncludes<T>(
        this IQueryable<T> query,
        QueryOptions options)
        where T : class
    {
        if (options?.FilteredIncludes == null || options.FilteredIncludes.Count == 0) return query;
        return IncludeBuilder.Apply(query, options);
    }
}
