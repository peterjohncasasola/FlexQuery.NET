using System.Data.Common;
using System.Text.Json;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Execution;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Serialization;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Mapping;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Extension methods for executing FlexQuery requests against a
/// <see cref="DbConnection"/> using Dapper.
/// Provides overloads accepting <see cref="FlexQueryParameters"/>,
/// raw query-string dictionaries, or pre-parsed <see cref="QueryOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Cancellation is observed during connection opening, diagnostics callbacks,
/// and result materialization.
/// </para>
/// <para>
/// Dapper's <c>QueryAsync</c> APIs do not currently accept a
/// <see cref="CancellationToken"/>. As a result, cancellation cannot interrupt
/// the database query once execution has started. A future version may support
/// this through <c>CommandDefinition</c>.
/// </para>
/// </remarks>
public static class FlexQueryDapperExtensions
{
    /// <summary>
    /// Parses the supplied <paramref name="parameters"/> and executes the
    /// resulting FlexQuery request against the database connection.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">The query parameters.</param>
    /// <param name="configure">
    /// Optional delegate used to configure Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configure?.Invoke(dapperOptions);

        var effectiveSyntax = dapperOptions.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var options = parameters.ToQueryOptions(effectiveSyntax);
        return await DapperQueryExecutor.RunAsync<T>(connection, options, dapperOptions, cancellationToken);
    }

    /// <summary>
    /// Converts raw query-string values into
    /// <see cref="FlexQueryParameters"/> and executes the query.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="parameters">The raw query-string key/value pairs.</param>
    /// <param name="configure">
    /// Optional delegate used to configure Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
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

        return await FlexQueryAsync<T>(connection, flexParams, configure, cancellationToken);
    }

    /// <summary>
    /// Executes a pre-parsed <see cref="QueryOptions"/> against the
    /// database connection.
    /// </summary>
    /// <typeparam name="T">The entity type used for mapping resolution.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="queryOptions">The pre-parsed query options.</param>
    /// <param name="options">
    /// Optional Dapper-specific execution options.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains the query result.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="connection"/> or
    /// <paramref name="queryOptions"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(queryOptions);

        var dapperOptions = options ?? new DapperQueryOptions();

        return await DapperQueryExecutor.RunAsync<T>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    /// <summary>
    /// Typed DTO overload: executes a FlexQuery against a Dapper connection and returns
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
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var dapperOptions = new DapperQueryOptions();
        configure?.Invoke(dapperOptions);

        var effectiveSyntax = dapperOptions.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var queryOptions = parameters.ToQueryOptions(effectiveSyntax);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        QueryOptions queryOptions,
        Action<DapperQueryOptions>? configure = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var dapperOptions = new DapperQueryOptions();
        configure?.Invoke(dapperOptions);

        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var effectiveSyntax = options.QuerySyntax ?? FlexQueryCore.DefaultOptions.DefaultQuerySyntax;
        var queryOptions = parameters.ToQueryOptions(effectiveSyntax);
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, options, cancellationToken);
    }

    public static async Task<QueryResult<TResponse>> FlexQueryAsync<TEntity, TResponse>(
        this DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        return await ExecuteTypedDtoAsync<TEntity, TResponse>(connection, queryOptions, options, cancellationToken);
    }

    private static async Task<QueryResult<TResponse>> ExecuteTypedDtoAsync<TEntity, TResponse>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions dapperOptions,
        CancellationToken cancellationToken)
        where TEntity : class
        where TResponse : class
    {
        var surface = QuerySurfaceBuilder.Build(typeof(TEntity), typeof(TResponse), dapperOptions);
        var ctx = new QueryContext { QuerySurface = surface, ExecutionOptions = dapperOptions, TargetType = typeof(TEntity) };

        queryOptions = queryOptions.Normalize();
        if (dapperOptions.DisablePaging) queryOptions.Paging.Disabled = true;

        queryOptions.ValidateOrThrow(ctx, dapperOptions);

        // Translate public include/expand paths to entity property names so the include
        // machinery (split queries, relationship resolution) operates on the entity graph.
        FieldResolver.TranslateIncludePathsToEntity(queryOptions, surface);

        // Build the result surface from PUBLIC field names before name translation.
        // Grouped shapes must be captured pre-rewrite so the output identity stays on
        // DTO names rather than the internal entity property names. Ungrouped
        // aggregates do not change the row shape — they flow to QueryResult.Aggregates.
        var resultShape = ResultShapeBuilder.Build(queryOptions.Select, surface);
        var isGrouped = queryOptions.GroupBy is { Count: > 0 };
        if (isGrouped && resultShape is null)
        {
            resultShape = ResultShapeBuilder.BuildGroupedShape(queryOptions);
        }

        DtoFieldNameRewriter.Rewrite(queryOptions, surface, dapperOptions.MappingRegistry);

        var hasIncludeExpand = (queryOptions.Includes?.Count > 0) || (queryOptions.Expand?.Count > 0);

        if (!hasIncludeExpand)
            return await DapperQueryExecutor
                .RunDtoAsync<TEntity, TResponse>(connection, queryOptions, dapperOptions, surface, resultShape, cancellationToken);
        
        var nonDtoResult = await DapperQueryExecutor.RunAsync<TEntity>(
            connection, queryOptions, dapperOptions, cancellationToken);

        // Prefer the mapping registry's TypeMap graph when the host registered one
        // (CreateMap/ForMember/ForNavigation): nested navigations materialize
        // recursively into DTO types — raw entity graphs never leak. Only the requested
        // include/expand navigation paths are materialized; DTO-declared deeper
        // navigations stay at their DTO default.
        var typeMap = dapperOptions.MappingRegistry?.Find(typeof(TEntity), typeof(TResponse));
        var allowedNavigationPaths = RequestedNavigationGraph.Collect(queryOptions);

        var data = new List<TResponse>(nonDtoResult.Data.Count);
        foreach (var item in nonDtoResult.Data)
        {
            if (typeMap is not null)
            {
                data.Add((TResponse)TypeMapMaterializer.Materialize(
                    typeMap, item, dapperOptions.MappingRegistry!, allowedNavigationPaths: allowedNavigationPaths));
                continue;
            }

            var json = JsonSerializer.Serialize(item);
            var dto = JsonSerializer.Deserialize<TResponse>(json);
            if (dto is not null)
                data.Add(dto);
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
