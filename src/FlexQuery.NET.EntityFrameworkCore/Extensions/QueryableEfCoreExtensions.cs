using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.EntityFrameworkCore.Execution;
using FlexQuery.NET.EntityFrameworkCore.Includes;
using FlexQuery.NET.Builders;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Serialization;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Projection;
using Microsoft.EntityFrameworkCore;

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
        var effectiveSyntax = options.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var queryOptions = parameters.ToQueryOptions(effectiveSyntax);
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

    /// <summary>
    /// Typed DTO overload: executes a FlexQuery against an EF Core queryable and returns
    /// strongly-typed <typeparamref name="TResponse"/> instances. DTO field names are resolved
    /// through <see cref="QuerySurface"/>; unmapped DTO properties fail at query time.
    /// </summary>
    /// <remarks>
    /// All standard FlexQuery capabilities (filter, sort, paging, keyset paging, select,
    /// aliases, total count, Include/Expand, GroupBy, aggregates, and governance) are
    /// supported. A feature fails only when the requested typed result shape is genuinely
    /// incompatible with <typeparamref name="TResponse"/>.
    /// </remarks>
    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        FlexQueryParameters parameters,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var options = ResolveOptions(configure);
        var effectiveSyntax = options.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var queryOptions = parameters.ToQueryOptions(effectiveSyntax);
        ThrowIfNull(query, queryOptions, options);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        QueryOptions queryOptions,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var options = ResolveOptions(configure);
        ThrowIfNull(query, queryOptions, options);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        FlexQueryParameters parameters,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var queryOptions = parameters.ToQueryOptions();
        ThrowIfNull(query, queryOptions, options);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        ThrowIfNull(query, queryOptions, options);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, options, cancellationToken);
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
    /// <remarks>
    /// Global options only override values they explicitly set: <c>null</c> means
    /// "use the package default" (e.g. <see cref="EfCoreQueryOptions.UseNoTracking"/>
    /// defaults to <c>true</c>), not "reset to EF's tracked behavior".
    /// </remarks>
    private static EfCoreQueryOptions ResolveOptions(Action<EfCoreQueryOptions>? configure)
    {
        var options = new EfCoreQueryOptions();
        var global = FlexQueryEFCore.DefaultOptions;
        if (global?.UseNoTracking != null)
            options.UseNoTracking = global.UseNoTracking;

        configure?.Invoke(options);
        return options;
    }

    private static async Task<QueryResult<TResponse>> ExecuteTypedDtoAsync<TEntity, TResponse>(
        IQueryable<TEntity> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken)
        where TEntity : class
        where TResponse : class
    {
        var surface = QuerySurfaceBuilder.Build(typeof(TEntity), typeof(TResponse), options);
        var ctx = new QueryContext { QuerySurface = surface, ExecutionOptions = options, TargetType = typeof(TEntity) };

        queryOptions = queryOptions.Normalize();
        if (options.DisablePaging) queryOptions.Paging.Disabled = true;

        queryOptions.ValidateOrThrow(ctx, options);

        // Honor the no-tracking contract on the typed DTO path exactly like the entity-only
        // executor. Without this, typed DTO queries run tracked: EF reuses change-tracker
        // instances (whose navigation collections are already populated), inverse navigations
        // are fixed up, and materialization is slower.
        if (options.UseNoTracking == true)
            query = query.AsNoTracking();

        // Capture public include/expand paths for the projection wiring, then translate
        // them to entity property names so navigation resolution (IncludeBuilder) and
        // validation-adjacent consumers operate on the entity graph.
        var publicIncludes = queryOptions.Includes?.ToList();
        var publicExpand = queryOptions.Expand;
        FieldResolver.TranslateIncludePathsToEntity(queryOptions, surface);

        var hasGroupBy = queryOptions.GroupBy is { Count: > 0 };
        var hasAggregates = queryOptions.Aggregates.Count > 0;

        var filtered = QueryBuilder.ApplyFilter(query, queryOptions);
        var total = options.IncludeTotalCount ? await filtered.CountAsync(cancellationToken) : (int?)null;

        if (hasGroupBy)
        {
            // Grouped queries have their own result shape (group keys + aggregates).
            // The typed executor projects into TResponse when the DTO models that shape,
            // and falls back to the dynamic grouped shape otherwise — aggregate aliases
            // are result metadata, not DTO row properties.
            return await TypedGroupedQueryExecutor.ExecuteAsync<TEntity, TResponse>(
                filtered, queryOptions, total, options, cancellationToken);
        }

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (hasAggregates)
        {
            // Ungrouped aggregates are grand totals over the filtered set: aggregate
            // metadata belongs to QueryResult.Aggregates, never to the row DTO. Rows
            // keep flowing through the normal DTO projection below.
            var aggregateQuery = GroupByBuilder.Apply(filtered, queryOptions);
            var aggregateRow = await aggregateQuery.FirstOrDefaultAsync(cancellationToken);
            grandTotals = AggregateResultBuilder.Build(aggregateRow, queryOptions.Aggregates);
        }

        filtered = QueryBuilder.ApplySort(filtered, queryOptions);
        filtered = queryOptions.IsKeysetMode
            ? QueryBuilder.ApplyKeysetPaging(filtered, queryOptions)
            : QueryBuilder.ApplyPaging(filtered, queryOptions);
        filtered = filtered.ApplyExpand(queryOptions);

        var fieldsToProject = queryOptions.Select is { Count: > 0 }
            ? queryOptions.Select.ToList()
            : surface.GetDefaultSelectFields().Select(f => new SelectNode { Field = f }).ToList();

        var hasIncludeExpand = queryOptions.Includes is { Count: > 0 } || queryOptions.Expand is { Count: > 0 };

        if (hasIncludeExpand)
        {
            var includedNavigations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var include in publicIncludes ?? [])
                includedNavigations.Add(include.Split('.').First());
            foreach (var expand in publicExpand ?? [])
                includedNavigations.Add(expand.Path.Split('.').First());

            var existingFields = new HashSet<string>(fieldsToProject.Select(f => f.Field), StringComparer.OrdinalIgnoreCase);
            foreach (var nav in includedNavigations)
            {
                if (existingFields.Contains(nav)) continue;
                if (!surface.TryResolve(nav, out var resolved) || !resolved.IsNavigation) continue;
                
                fieldsToProject.Add(new SelectNode { Field = nav });
                existingFields.Add(nav);
            }
        }

        IReadOnlyList<TResponse> data;
        if (hasIncludeExpand && options.UseNoTracking == true)
        {
            // Server-side DTO projection with expansion windows embedded in the
            // expression tree. Every expansion level applies its own filter/sort/take to
            // its navigation body, correlated to the already-selected parent element —
            // the child collection is restricted to the selected parent rows (correlated
            // OUTER APPLY on SQL Server), so child processing is driven by the relevant
            // parent graph instead of the total size of the child table. Only selected
            // root DTO columns are read.
            if (!IncludeBuilder.TryBuildExpandTree(queryOptions, out var expandWindows))
            {
                expandWindows = new Dictionary<string, ExpandWindowNode>(StringComparer.OrdinalIgnoreCase);
            }

            var lambda = DtoProjectionBuilder.Build<TEntity, TResponse>(
                queryOptions, surface, fieldsToProject, expandWindows: expandWindows, mappingRegistry: options.MappingRegistry);
            var correlated = filtered.Select(lambda);

            // Providers without APPLY support (e.g. SQLite) cannot translate nested
            // per-parent takes inside the projection; fall back to the filtered-include
            // chain (provider-canonical correlated windowed join) + client mapping.
            try
            {
                _ = correlated.ToQueryString();
                data = await correlated.ToListAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("APPLY", StringComparison.OrdinalIgnoreCase))
            {
                var entities = await filtered.ToListAsync(cancellationToken);
                var fallbackLambda = DtoProjectionBuilder.Build<TEntity, TResponse>(
                    queryOptions, surface, fieldsToProject, mappingRegistry: options.MappingRegistry);
                var func = fallbackLambda.Compile();
                data = entities.Select(func).ToList();
            }
        }
        else if (hasIncludeExpand)
        {
            // Tracked mode: materialize entities through the EF filtered-include chain —
            // the chain applies every expansion level's filter/sort/take correlated to
            // its parent — then map client-side.
            var entities = await filtered.ToListAsync(cancellationToken);
            var lambda = DtoProjectionBuilder.Build<TEntity, TResponse>(
                queryOptions, surface, fieldsToProject, mappingRegistry: options.MappingRegistry);
            var func = lambda.Compile();
            data = entities.Select(func).ToList();
        }
        else
        {
            var projected = filtered.ApplyDtoSelect<TEntity, TResponse>(queryOptions, surface, fieldsToProject, options.MappingRegistry);
            data = await projected.ToListAsync(cancellationToken);
        }

        // Shared result shaping also builds the keyset cursor token from the public
        // (TResponse) field names when keyset paging is active.
        var result = queryOptions.BuildQueryResult(data, total, aggregates: grandTotals);

        // With includes/expands, the effective projection includes the navigation heads
        // (fieldsToProject) — the serialized surface must expose them under their public
        // names alongside the explicit root select.
        result.ResultShape = ResultShapeBuilder.Build(
            hasIncludeExpand ? fieldsToProject : queryOptions.Select, surface);
        return result;
    }
}
