using System.Data.Common;
using System.Text.Json;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Execution;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Serialization;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Dapper;

public static class FlexQueryDapperExtensions
{
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        FlexQueryOptions? global = null,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        global ??= new FlexQueryOptions();
        var dapperOptions = ResolveOptions(global, configure);
        var options = parameters.ToQueryOptions(dapperOptions.QuerySyntax ?? global.DefaultQuerySyntax);
        global.Freeze();
        return await DapperQueryExecutor.RunAsync<T>(connection, options, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        FlexQueryOptions? global = null,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var dict = parameters.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var flexParams = new FlexQueryParameters
        {
            Filter = dict.GetValueOrDefault(QueryOptionKeys.Filter) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Filter}"),
            Sort = dict.GetValueOrDefault(QueryOptionKeys.Sort) ?? dict.GetValueOrDefault(QueryOptionKeys.OrderBy) ?? dict.GetValueOrDefault($"${QueryOptionKeys.OrderBy}"),
            Select = dict.GetValueOrDefault(QueryOptionKeys.Select) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Select}"),
            Include = dict.GetValueOrDefault(QueryOptionKeys.Include) ?? dict.GetValueOrDefault(QueryOptionKeys.Expand) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Expand}"),
            Expand = dict.GetValueOrDefault(QueryOptionKeys.Expand) ?? dict.GetValueOrDefault($"${QueryOptionKeys.Expand}"),
            Page = dict.TryGetValue(QueryOptionKeys.Page, out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = dict.TryGetValue(QueryOptionKeys.PageSize, out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null,
            PreserveRawOrder = true
        };
        return await FlexQueryAsync<T>(connection, flexParams, global, configure, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions queryOptions,
        FlexQueryOptions? global = null,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        global ??= new FlexQueryOptions();
        var dapperOptions = ResolveOptions(global, configure);
        global.Freeze();
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(queryOptions);
        return await DapperQueryExecutor.RunAsync<T>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(queryOptions);
        return await DapperQueryExecutor.RunAsync<T>(connection, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        FlexQueryOptions? global = null,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        global ??= new FlexQueryOptions();
        var dapperOptions = ResolveOptions(global, configure);
        var queryOptions = parameters.ToQueryOptions(dapperOptions.QuerySyntax ?? global.DefaultQuerySyntax);
        global.Freeze();
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        QueryOptions queryOptions,
        FlexQueryOptions? global = null,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        global ??= new FlexQueryOptions();
        var dapperOptions = ResolveOptions(global, configure);
        global.Freeze();
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
    {
        var queryOptions = parameters.ToQueryOptions(options.QuerySyntax ?? QuerySyntax.NativeDsl);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class where TResponse : class
        => await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, options, cancellationToken);

    private static DapperQueryOptions ResolveOptions(FlexQueryOptions global, Action<DapperQueryOptions>? configure)
    {
        var options = new DapperQueryOptions();
        global.ApplyTo(options);
        configure?.Invoke(options);
        return options;
    }

    private static async Task<QueryResult<TResponse>> ExecuteTypedDtoAsync<TEntity, TResponse>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions dapperOptions,
        CancellationToken cancellationToken)
        where TEntity : class where TResponse : class
    {
        var surface = QuerySurfaceBuilder.Build(typeof(TEntity), typeof(TResponse), dapperOptions);
        var ctx = new QueryContext { QuerySurface = surface, ExecutionOptions = dapperOptions, TargetType = typeof(TEntity) };
        queryOptions = queryOptions.Normalize();
        if (dapperOptions.DisablePaging) queryOptions.Paging.Disabled = true;
        queryOptions.ValidateOrThrow(ctx, dapperOptions);

        FieldResolver.TranslateIncludePathsToEntity(queryOptions, surface);
        var resultShape = ResultShapeBuilder.Build(queryOptions.Select, surface);
        if (queryOptions.GroupBy is { Count: > 0 } && resultShape is null)
            resultShape = ResultShapeBuilder.BuildGroupedShape(queryOptions);

        DtoFieldNameRewriter.Rewrite(queryOptions, surface, dapperOptions.MappingRegistry);
        var hasIncludeExpand = (queryOptions.Includes?.Count > 0) || (queryOptions.Expand?.Count > 0);

        if (!hasIncludeExpand)
            return await DapperQueryExecutor.RunDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, surface, resultShape, cancellationToken);

        var nonDtoResult = await DapperQueryExecutor.RunAsync<TEntity>(connection, queryOptions, dapperOptions, cancellationToken);
        var typeMap = dapperOptions.MappingRegistry?.Find(typeof(TEntity), typeof(TResponse));
        var allowedNavigationPaths = RequestedNavigationGraph.Collect(queryOptions);
        var data = new List<TResponse>(nonDtoResult.Data.Count);
        foreach (var item in nonDtoResult.Data)
        {
            if (typeMap is not null)
            {
                data.Add((TResponse)TypeMapMaterializer.Materialize(typeMap, item, dapperOptions.MappingRegistry!, allowedNavigationPaths: allowedNavigationPaths));
                continue;
            }

            var json = JsonSerializer.Serialize(item);
            var dto = JsonSerializer.Deserialize<TResponse>(json);
            if (dto is not null) data.Add(dto);
        }

        return new QueryResult<TResponse>
        {
            Data = data,
            TotalCount = nonDtoResult.TotalCount,
            ResultCount = nonDtoResult.ResultCount,
            Page = queryOptions.Paging.Page > 0 ? queryOptions.Paging.Page : 1,
            PageSize = queryOptions.Paging.PageSize > 0 ? queryOptions.Paging.PageSize : dapperOptions.DefaultPageSize,
            NextCursorToken = nonDtoResult.NextCursorToken,
            ResultShape = resultShape
        };
    }
}
