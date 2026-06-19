using FlexQuery.NET.Builders;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Threading;

namespace FlexQuery.NET.EFCore;

/// <summary>
/// EF Core-specific async extensions for materializing query results.
/// </summary>
public static class QueryableEfCoreExtensions
{

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

    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        Action<QueryExecutionOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var exec = new QueryExecutionOptions();
        configure?.Invoke(exec);

        return await query.FlexQueryAsync(parameters, exec, cancellationToken);
    }

    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="execOptions">Server-side security and execution rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        QueryExecutionOptions execOptions,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var options = QueryOptionsParser.Parse(parameters);

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        options.ValidateOrThrow<T>(execOptions);

        var hasProjection = options.HasProjection();

        return await query.ApplyFlexQueryAsync(options, hasProjection, execOptions, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against an EF Core queryable
    /// and returns a paged result set asynchronously.
    /// </summary>
    /// <remarks>
    /// Use this overload when composing with adapter packages (e.g. FlexQuery.NET.AgGrid,
    /// FlexQuery.NET.MiniOData) that parse external formats into <see cref="QueryOptions"/>.
    /// <code>
    /// // Step 1: Parse (adapter package)
    /// var options = AgGridQueryOptionsParser.Parse(agGridRequest);
    ///
    /// // Step 2: Execute (EF Core package)
    /// var result = await dbContext.Entities.FlexQueryAsync(options);
    /// </code>
    /// </remarks>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        Action<QueryExecutionOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var execOptions = new QueryExecutionOptions();
        configure?.Invoke(execOptions);

        return await query.FlexQueryAsync(options, execOptions, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against an EF Core queryable
    /// and returns a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="options">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="execOptions">Server-side security and execution rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(execOptions);

        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        options.ValidateOrThrow<T>(execOptions);

        var hasProjection = options.HasProjection();

        return await query.ApplyFlexQueryAsync(options, hasProjection, execOptions, cancellationToken);
    }

    private static async Task<QueryResult<object>> ApplyFlexQueryAsync<T>(this IQueryable<T> query, 
        QueryOptions options, bool hasProjection, QueryExecutionOptions? execOptions = null, CancellationToken cancellationToken = default)
        where T : class
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();

        if (execOptions?.UseNoTracking == true)
        {
            query = query.AsNoTracking();
        }

        var filtered = QueryBuilder.ApplyFilter(query, options);
        filtered = QueryBuilder.ApplySort(filtered, options);

        var total = options.IncludeCount == true ? await filtered.CountAsync(cancellationToken) : (int?)null;

        filtered = QueryBuilder.ApplyPaging(filtered, options);



        // Note: ApplySelect already incorporates FilteredIncludes filters into the projection tree,
        // so calling ApplyFilteredIncludes here is technically redundant but ensures consistency
        // if the projection engine behavior changes.
        filtered = filtered.ApplyFilteredIncludes(options);

        if (hasProjection)
        {
            var projectedData = await filtered.ApplySelect(options).ToListAsync(cancellationToken);
            return options.BuildQueryResult(projectedData, total); // QueryResult<object>
        }

        
        var filteredData = await filtered.ToListAsync(cancellationToken);

        //convert from QueryResult<T> to QueryResult<object> by treating each T as an object (no projection)
        return options.BuildQueryResult(filteredData, total).ToObjectResult();
    }

}
