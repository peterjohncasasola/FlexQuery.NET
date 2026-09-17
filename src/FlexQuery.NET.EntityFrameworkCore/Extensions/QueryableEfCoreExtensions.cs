using FlexQuery.NET.Configuration;
using FlexQuery.NET.EntityFrameworkCore.Execution;
using FlexQuery.NET.EntityFrameworkCore.Includes;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Resolvers;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore;

public static class QueryableEfCoreExtensions
{
    public static IQueryable<T> ApplyExpand<T>(this IQueryable<T> query, QueryOptions options) where T : class
        => IncludeBuilder.Apply(query, options);

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        FlexQueryOptions? global = null,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        global ??= new FlexQueryOptions();
        var options = ResolveOptions(global, configure);
        var queryOptions = parameters.ToQueryOptions(options.QuerySyntax ?? global.DefaultQuerySyntax);
        return await query.FlexQueryAsync(queryOptions, global, options, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default) where T : class
    {
        var queryOptions = parameters.ToQueryOptions(options.QuerySyntax ?? QuerySyntax.NativeDsl);
        ThrowIfNull(query, queryOptions, options);
        return await FlexQueryEfCoreExecutor.RunAsync(query, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        FlexQueryOptions? global = null,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        global ??= new FlexQueryOptions();
        var options = ResolveOptions(global, configure);
        ThrowIfNull(query, queryOptions, options);
        return await query.FlexQueryAsync(queryOptions, global, options, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfNull(query, queryOptions, options);
        return await FlexQueryEfCoreExecutor.RunAsync(query, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        FlexQueryParameters parameters,
        FlexQueryOptions? global = null,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        global ??= new FlexQueryOptions();
        var options = ResolveOptions(global, configure);
        var queryOptions = parameters.ToQueryOptions(options.QuerySyntax ?? global.DefaultQuerySyntax);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, global, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        QueryOptions queryOptions,
        FlexQueryOptions? global = null,
        Action<EfCoreQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        global ??= new FlexQueryOptions();
        var options = ResolveOptions(global, configure);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, global, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        FlexQueryParameters parameters,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        var queryOptions = parameters.ToQueryOptions(options.QuerySyntax ?? QuerySyntax.NativeDsl);
        ThrowIfNull(query, queryOptions, options);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, new FlexQueryOptions(), options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        QueryOptions queryOptions,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        ThrowIfNull(query, queryOptions, options);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(query, queryOptions, new FlexQueryOptions(), options, cancellationToken);
    }

    private static EfCoreQueryOptions ResolveOptions(FlexQueryOptions global, Action<EfCoreQueryOptions>? configure)
    {
        var options = new EfCoreQueryOptions();
        global.ApplyTo(options);
        configure?.Invoke(options);
        return options;
    }

    private static void ThrowIfNull(IQueryable queryable, QueryOptions queryOptions, EfCoreQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(queryOptions);
        ArgumentNullException.ThrowIfNull(options);
    }

    private static async Task<QueryResult<TResponse>> ExecuteTypedDtoAsync<TEntity, TResponse>(
        IQueryable<TEntity> query,
        QueryOptions queryOptions,
        FlexQueryOptions global,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken)
        where TEntity : class where TResponse : class
    {
        global.Freeze();
        var surface = QuerySurfaceBuilder.Build(typeof(TEntity), typeof(TResponse), options);
        var ctx = new QueryContext { QuerySurface = surface, ExecutionOptions = options, TargetType = typeof(TEntity) };

        queryOptions = queryOptions.Normalize();
        if (options.DisablePaging) queryOptions.Paging.Disabled = true;
        queryOptions.ValidateOrThrow(ctx, options);

        if (options.UseNoTracking == true)
            query = query.AsNoTracking();

        var publicIncludes = queryOptions.Includes?.ToList();
        var publicExpand = queryOptions.Expand;
        FieldResolver.TranslateIncludePathsToEntity(queryOptions, surface);

        var hasGroupBy = queryOptions.GroupBy is { Count: > 0 };
        var hasAggregates = queryOptions.Aggregates.Count > 0;
        var filtered = QueryBuilder.ApplyFilter(query, queryOptions);
        var total = options.IncludeTotalCount ? await filtered.CountAsync(cancellationToken) : (int?)null;

        if (hasGroupBy)
            return await TypedGroupedQueryExecutor.ExecuteAsync<TEntity, TResponse>(filtered, queryOptions, total, options, cancellationToken);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (hasAggregates)
        {
            var aggregateQuery = GroupByBuilder.Apply(filtered, queryOptions);
            var aggregateRow = await aggregateQuery.FirstOrDefaultAsync(cancellationToken);
            grandTotals = AggregateResultBuilder.Build(aggregateRow, queryOptions.Aggregates);
        }

        filtered = QueryBuilder.ApplySort(filtered, queryOptions);
        filtered = queryOptions.IsKeysetMode ? QueryBuilder.ApplyKeysetPaging(filtered, queryOptions) : QueryBuilder.ApplyPaging(filtered, queryOptions);
        filtered = filtered.ApplyExpand(queryOptions);

        var fieldsToProject = queryOptions.Select is { Count: > 0 }
            ? queryOptions.Select.ToList()
            : surface.GetDefaultSelectFields().Select(f => new SelectNode { Field = f }).ToList();

        var hasIncludeExpand = queryOptions.Includes is { Count: > 0 } || queryOptions.Expand is { Count: > 0 };
        if (hasIncludeExpand)
        {
            var includedNavigations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var include in publicIncludes ?? []) includedNavigations.Add(include.Split('.').First());
            foreach (var expand in publicExpand ?? []) includedNavigations.Add(expand.Path.Split('.').First());
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
            if (!IncludeBuilder.TryBuildExpandTree(queryOptions, out var expandWindows))
                expandWindows = new Dictionary<string, ExpandWindowNode>(StringComparer.OrdinalIgnoreCase);

            var lambda = DtoProjectionBuilder.Build<TEntity, TResponse>(queryOptions, surface, fieldsToProject, expandWindows: expandWindows, mappingRegistry: options.MappingRegistry);
            var correlated = filtered.Select(lambda);
            try
            {
                _ = correlated.ToQueryString();
                data = await correlated.ToListAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("APPLY", StringComparison.OrdinalIgnoreCase))
            {
                var entities = await filtered.ToListAsync(cancellationToken);
                var fallbackLambda = DtoProjectionBuilder.Build<TEntity, TResponse>(queryOptions, surface, fieldsToProject, mappingRegistry: options.MappingRegistry);
                data = entities.Select(fallbackLambda.Compile()).ToList();
            }
        }
        else if (hasIncludeExpand)
        {
            var entities = await filtered.ToListAsync(cancellationToken);
            var lambda = DtoProjectionBuilder.Build<TEntity, TResponse>(queryOptions, surface, fieldsToProject, mappingRegistry: options.MappingRegistry);
            data = entities.Select(lambda.Compile()).ToList();
        }
        else
        {
            var projected = filtered.ApplyDtoSelect<TEntity, TResponse>(queryOptions, surface, fieldsToProject, options.MappingRegistry);
            data = await projected.ToListAsync(cancellationToken);
        }

        var result = queryOptions.BuildQueryResult(data, total, aggregates: grandTotals);
        result.ResultShape = ResultShapeBuilder.Build(hasIncludeExpand ? fieldsToProject : queryOptions.Select, surface);
        return result;
    }
}
