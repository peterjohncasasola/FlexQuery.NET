using FlexQuery.NET.EntityFrameworkCore.Configuration;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.EntityFrameworkCore.Execution;
using FlexQuery.NET.EntityFrameworkCore.Includes;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// EF Core-specific async extensions for materializing query results.
/// </summary>
public static class QueryableEfCoreExtensions
{

    /// <summary>
    /// Applies the <b>Include Pipeline</b>: translates every
    /// <see cref="QueryOptions.Expand"/>
    /// into EF Core <c>Include</c> / <c>ThenInclude</c> calls, each optionally
    /// filtered by an inline <c>Where</c> clause.
    ///
    /// <para>
    /// This pipeline is <b>completely independent</b> of the WHERE pipeline
    /// (<c>FlexQueryAsync</c>).
    /// It must be called <em>before</em> any materialization (e.g.
    /// <c>ToListAsync</c>) but after <c>ApplyQueryOptions</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var options = QueryOptionsParser.Parse(Request.Query);
    ///
    /// var result = await _context.Customers
    ///     .ApplyQueryOptions(options)       // WHERE pipeline
    ///     .ApplyExpand(options)       // INCLUDE pipeline
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public static IQueryable<T> ApplyExpand<T>(
        this IQueryable<T> query,
        QueryOptions options)
        where T : class
    {
        if (options?.Includes == null || options.Includes.Count == 0) return query;
        return IncludeBuilder.Apply(query, options);
    }

    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is cancelled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var options = ResolveOptions(configure);
        var queryOptions = parameters.ToQueryOptions();
        ThrowIfNull(query, queryOptions, options);

        return await query.FlexQueryAsync(parameters, options, cancellationToken);
    }
    
    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, validates it against server rules,
    /// and applies it to the query to return a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="options">Server-side security and execution rules.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is cancelled.</exception>

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var queryOptions = parameters.ToQueryOptions();
        ThrowIfNull(query, queryOptions, options);
 
        return await FlexQueryEfCoreExecutor.RunAsync(
            query, queryOptions, options, cancellationToken);
    }
    
    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against an EF Core queryable
    /// and returns a paged result set asynchronously.
    /// </summary>
    /// <remarks>
    /// Use this overload when composing with adapter packages (e.g. FlexQueryAsync.NET.Adapters.AgGrid,
    /// FlexQueryAsync.NET.Parsers.MiniOData) that parse external formats into <see cref="QueryOptions"/>.
    /// <code>
    /// // Step 1: Parse (adapter package)
    /// var options = AgGridQueryOptionsParser.Parse(agGridRequest);
    ///
    /// // Step 2: Execute (EF Core package)
    /// var result = await dbContext.Entities.FlexQueryAsync(options);
    /// </code>
    /// </remarks>
    /// <param name="query">The source queryable.</param>
    /// <param name="queryOptions">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="configure">Optional configuration for server-side security and execution rules.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is cancelled.</exception>

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var options = ResolveOptions(configure);
        ThrowIfNull(query, queryOptions, options);

        return await query.FlexQueryAsync(queryOptions, options, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against an EF Core queryable
    /// and returns a paged result set asynchronously.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="queryOptions">Pre-parsed query options from any adapter or manual construction.</param>
    /// <param name="options">Server-side security and execution rules.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is cancelled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ThrowIfNull(query, queryOptions,  options);
        
        return await FlexQueryEfCoreExecutor.RunAsync(
            query, queryOptions, options, cancellationToken);
    }

    private static void ThrowIfNull(IQueryable queryable, QueryOptions queryOptions, EfCoreQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(queryOptions);
        ArgumentNullException.ThrowIfNull(options);
    }

    /// <summary>
    /// Resolves the EF Core execution options for a request using the precedence
    /// per-execution configuration &gt; global configuration &gt; package defaults.
    /// </summary>
    private static EfCoreQueryOptions ResolveOptions(Action<EfCoreQueryOptions>? configure)
    {
        var options = FlexQueryEFCore.DefaultOptions is { } global
            ? new EfCoreQueryOptions
            {
                UseNoTracking = global.UseNoTracking,
            }
            : new EfCoreQueryOptions();

        configure?.Invoke(options);
        return options;
    }
}
