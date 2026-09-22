using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Dapper;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Context;
using FlexQuery.NET.Dapper.Diagnostics;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Materialization;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Adapters;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Sql.Utilities;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Serialization;
using Microsoft.Extensions.Logging;

namespace FlexQuery.NET.Dapper.Execution;

internal static class DapperQueryExecutor
{
    private static string? ApplyNaming(string? name, Func<string, string>? transformer)
        => string.IsNullOrEmpty(name) ? name : transformer?.Invoke(name) ?? name;

    public static async Task<QueryResult<object>> RunAsync<T>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        queryOptions.Items[ContextKeys.EntityType] = typeof(T);

        queryOptions = queryOptions.Normalize();

        if (options.DisablePaging)
            queryOptions.Paging.Disabled = true;

        queryOptions.ValidateOrThrow(typeof(T), options);

        var listener = options.Listener;
        
        var ctx = listener is not null
            ? new FlexQueryExecutionContext(listener, cancellationToken)
            : null;

        await ctx.NotifyParsedAsync(queryOptions);
        
        var sqlLogger = options.LoggerFactory?.CreateLogger(DapperSqlLog.CategoryName);
        
        var context = new DapperQueryContext
        {
            Connection = connection,
            QueryOptions = queryOptions,
            DapperQueryOptions = options,
            SqlLogger = sqlLogger,
            CancellationToken = cancellationToken
        };

        return await ExecuteAsync<T>(context, ctx);
    }

    public static async Task<QueryResult<TResponse>> RunDtoAsync<TEntity, TResponse>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        IQuerySurface surface,
        IReadOnlyList<SelectOutputField>? resultShape = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var ctx = options.Listener is not null
            ? new FlexQueryExecutionContext(options.Listener, cancellationToken)
            : null;

        queryOptions.Items[ContextKeys.EntityType] = typeof(TEntity);

        await ctx.NotifyParsedAsync(queryOptions);

        var sqlLogger = options.LoggerFactory?.CreateLogger(DapperSqlLog.CategoryName);

        var ct = ctx?.CancellationToken ?? cancellationToken;
        await ConnectionHelper.EnsureOpenAsync(connection, ct);

        var dialect = SqlDialectResolver.Resolve(connection);
        var registry = options.Model?.Registry
            ?? FlexQueryDapper.DefaultModel?.Registry
            ?? new MappingRegistry();

        var mapping = registry.GetMapping(typeof(TEntity));
        var translator = new SqlTranslator(registry, dialect);

        // Aggregates without GROUP BY are grand totals: the SQL layer handles them with a
        // dedicated single-row aggregate query and the result flows into
        // QueryResult.Aggregates â€” never into the DTO row shape.
        var isGrouped = queryOptions.GroupBy is { Count: > 0 };

        var command = translator.Translate(BuildRootOnlyOptions(queryOptions));
        var parameters = CommandParameterAdapter.ToDynamicParameters(command);

        if (ctx is not null)
        {
            var queryParameters =
                command.Parameters.Select(p => new QueryParameter(p.Key, p.Value)).ToList().AsReadOnly();
            await ctx.NotifyTranslatedAsync(command.Sql, queryParameters);
        }

        DapperSqlLog.Command(sqlLogger, command.Sql, command.Parameters);

        var rows = await connection.QueryAsync(new CommandDefinition(
            command.Sql,
            parameters,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text,
            cancellationToken: ct));

        var rowsList = rows.ToList();
        
        var context = new DapperQueryContext
        {
            Connection = connection,
            QueryOptions = queryOptions,
            SqlTranslator = translator,
            SqlCommand = command,
            Parameters = parameters,
            DapperQueryOptions = options,
            SqlLogger = sqlLogger,
            CancellationToken = ct
        };

        IReadOnlyList<TResponse> items;
        if (isGrouped)
        {
            // Grouped queries have their own result shape (group keys + aliases): project
            // into TResponse when the DTO models that shape, otherwise fall back to the
            // dynamic grouped flow â€” aggregate aliases are result metadata, not row
            // properties, and must not be required on the DTO.
            if (!CanRepresentGroupedShape<TResponse>(queryOptions, surface))
            {
                var dynamicResult = await ExecuteAsync<TEntity>(context, ctx);
                var groupedShape = resultShape ?? ResultShapeBuilder.BuildGroupedShape(queryOptions);

                var wrapped = new QueryResult<TResponse>
                {
                    Data = DynamicGroupedResult.WrapData<TResponse>(dynamicResult.Data),
                    TotalCount = dynamicResult.TotalCount,
                    ResultCount = dynamicResult.ResultCount,
                    Page = dynamicResult.Page,
                    PageSize = dynamicResult.PageSize,
                    NextCursorToken = dynamicResult.NextCursorToken,
                    Aggregates = dynamicResult.Aggregates,
                    ResultShape = groupedShape
                };

                await ctx.NotifyMaterializedAsync(wrapped);
                return wrapped;
            }

            items = DapperResultMaterializer.MaterializeGroupedDto<TResponse>(rowsList, queryOptions, surface);
        }
        else
        {
            items = DapperResultMaterializer.MaterializeDto<TResponse>(
                rowsList,
                queryOptions,
                surface,
                registry,
                typeof(TEntity));
        }
        
        var (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(context);

        // Grand totals ride the existing aggregate metadata channel (QueryResult.Aggregates).
        // Aggregate metadata keys use the PUBLIC field identity (DTO names) in DTO mode.
        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(context, Resolver);

        await ctx.NotifyExecutedAsync(items.Count);

        var effectiveShape = isGrouped
            ? (resultShape ?? ResultShapeBuilder.BuildGroupedShape(queryOptions))
            : resultShape;

        var queryResult = queryOptions.BuildQueryResult(data: items, totalCount, aggregates: grandTotals, resultCount);
        queryResult.ResultShape = effectiveShape;

        await ctx.NotifyMaterializedAsync(queryResult);

        return queryResult;

        string Resolver(string fieldName) =>
            surface.TryResolveByEntityName(fieldName, out var resolved)
                ? resolved.SurfaceName
                : fieldName;
    }

    /// <summary>
    /// Returns true when the response DTO models the full grouped result shape â€”
    /// every group key and aggregate alias has a writable property. Group fields arrive
    /// rewritten as entity names and are mapped back to their public DTO surface names.
    /// </summary>
    private static bool CanRepresentGroupedShape<TResponse>(QueryOptions queryOptions, IQuerySurface surface)
        where TResponse : class
    {
        var writableProps = typeof(TResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var groupField in queryOptions.GroupBy ?? [])
        {
            var publicName = surface.TryResolveByEntityName(groupField, out var resolved)
                ? resolved.SurfaceName
                : groupField;
            if (!writableProps.ContainsKey(GroupByBuilder.GetProjectionName(publicName)))
                return false;
        }

        foreach (var aggregate in queryOptions.Aggregates)
        {
            if (!writableProps.ContainsKey(aggregate.Alias))
                return false;
        }

        return true;
    }

    private static async Task<QueryResult<object>> ExecuteAsync<T>(
        DapperQueryContext queryContext,
        FlexQueryExecutionContext? ctx)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? queryContext.CancellationToken;
        var connection = queryContext.Connection;
        var dapperQueryOptions =  queryContext.DapperQueryOptions;
        var queryOptions = queryContext.QueryOptions;
        var sqlLogger =  queryContext.SqlLogger;
        
        await ConnectionHelper.EnsureOpenAsync(connection, ct);

        var dialect = SqlDialectResolver.Resolve(connection);
        var registry = dapperQueryOptions.Model?.Registry
            ?? FlexQueryDapper.DefaultModel?.Registry
            ?? new MappingRegistry();

        queryOptions.Items[ContextKeys.EntityType] = typeof(T);

        var transformer = queryOptions.Items.TryGetValue(ContextKeys.PropertyNameTransformer, out var tObj)
            && tObj is Func<string, string> transformerFunc
            ? transformerFunc
            : null;

        var mapping = registry.GetMapping(typeof(T));
        var translator = new SqlTranslator(registry, dialect);
        var useSimpleIncludeStreaming = SqlSimpleIncludeQueryBuilder.CanBuild(queryOptions, mapping, registry)
            && (!dialect.RequiresOrderByForPaging || !queryOptions.Paging.Disabled || queryOptions.Sort.Count == 0);
        var simpleIncludeCommand = useSimpleIncludeStreaming
            ? SqlSimpleIncludeQueryBuilder.Build(queryOptions, mapping, registry, dialect, translator)
            : null;
        var command = useSimpleIncludeStreaming
            ? new SqlCommand
            {
                Sql = simpleIncludeCommand!.Sql,
                Parameters = simpleIncludeCommand.Parameters,
                ColumnAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }
            : translator.Translate(BuildRootOnlyOptions(queryOptions));

        var parameters = useSimpleIncludeStreaming
            ? null
            : CommandParameterAdapter.ToDynamicParameters(command);

        if (ctx is not null)
        {
            var queryParameters =
                command.Parameters.Select(p => new QueryParameter(p.Key, p.Value)).ToList().AsReadOnly();
            await ctx.NotifyTranslatedAsync(command.Sql, queryParameters);
        }

        // Flat projection modes deliver leaf columns through the single-query JOIN in the
        // root SQL includes exist only to satisfy the navigation-include authorization
        // contract and must not trigger navigation hydration/split queries.
        var isFlatProjection =
            queryOptions.ProjectionMode is ProjectionMode.Flat or ProjectionMode.FlatMixed
            && queryOptions.HasProjection();

        var hasNavigation = !isFlatProjection &&
                            (queryOptions.Includes?.Count > 0);
        IReadOnlyList<object> items;

        var splitIncludeContext = new SplitIncludeContext
        {
            Mapping = mapping,
            QueryOptions = queryOptions,
            Parameters = parameters,
            ParameterContext = new SqlParameterContext(dialect),
            Dialect =  dialect,
            Registry =  registry,
            CancellationToken = ct,
            Connection = connection,
            SqlLogger = sqlLogger,
            SqlTranslator =  translator,
            DapperQueryOptions = dapperQueryOptions,
        };


        if (useSimpleIncludeStreaming)
        {
            var selectTree = SelectTreeBuilder.Build(queryOptions);
            var projectedResult = await SimpleIncludeStreamingMaterializer.MaterializeProjectedAsync(
                connection,
                simpleIncludeCommand!,
                mapping,
                dapperQueryOptions.CommandTimeout,
                selectTree,
                ct,
                sqlLogger);

            items = projectedResult.Items;
        }
        else
        {
            DapperSqlLog.Command(sqlLogger, command.Sql, command.Parameters);

            var rows = await connection.QueryAsync(new CommandDefinition(
                command.Sql,
                parameters!,
                commandTimeout: dapperQueryOptions.CommandTimeout,
                commandType: CommandType.Text,
                cancellationToken: ct));

            var rowsList = rows.ToList();

            if (hasNavigation)
            {
                items = await ExecuteSplitQueryAsync<T>(
                    splitIncludeContext,
                    rowsList,
                    command.ColumnAliasMap,
                    transformer);
            }
            else
            {
                items = DapperResultMaterializer.Materialize(
                    rowsList,
                    queryOptions,
                    hydrateIncludes: _ => [],
                    cancellationToken: ct,
                    propertyNameTransformer: transformer,
                    entityType: typeof(T));
            }
        }
        
        
        var context = new DapperQueryContext
        {
            Connection = connection,
            QueryOptions = queryOptions,
            SqlTranslator = translator,
            SqlCommand = command,
            Parameters = parameters,
            DapperQueryOptions = dapperQueryOptions,
            SqlLogger = sqlLogger,
            CancellationToken = ct
        };

        var (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(context);

        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(context);

        if (transformer != null && grandTotals != null)
        {
            grandTotals = grandTotals.ToDictionary(
                outer => ApplyNaming(outer.Key, transformer) ?? outer.Key,
                outer => outer.Value.ToDictionary(
                    inner => ApplyNaming(inner.Key, transformer) ?? inner.Key,
                    inner => inner.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        await ctx.NotifyExecutedAsync(items.Count);
        
        var queryResult = queryOptions.BuildQueryResult(data: items, totalCount, aggregates: grandTotals, resultCount);
        
        await ctx.NotifyMaterializedAsync(queryResult);

        return queryResult;
    }

    private static QueryOptions BuildRootOnlyOptions(QueryOptions queryOptions)
    {
        if ((queryOptions.Includes?.Count ?? 0) == 0)
        {
            return queryOptions;
        }

        // Flat projection modes deliver leaf columns through the single-query JOIN â€” the
        // select shape (including navigation leaf paths) must survive, otherwise the root
        // SQL degrades to a full root-column scan and the flat output is lost.
        var isFlatProjection =
            queryOptions.ProjectionMode is ProjectionMode.Flat or ProjectionMode.FlatMixed
            && queryOptions.HasProjection();

        var rootOptions = queryOptions.CopyQueryOptions();
        rootOptions.Includes = null;
        if (!isFlatProjection)
        {
            rootOptions.Select = null;
            rootOptions.SelectTree = null;
        }
        return rootOptions;
    }

    private static async Task<IReadOnlyList<object>> ExecuteSplitQueryAsync<T>(
        SplitIncludeContext context,
        IReadOnlyList<dynamic> rowsList,
        Dictionary<string, string>? columnAliasMap,
        Func<string, string>? transformer)
        where T : class
    {
        
        var queryOptions = context.QueryOptions;
        var mapping = context.Mapping;
        var registry =  context.Registry;
        
        var rootItems = DapperRowHydrator.HydrateCore<T>(
            rowsList, mapping, registry, new List<string> { string.Empty }, columnAliasMap);

        if (rootItems.Count == 0)
            return Array.Empty<object>();

        if (queryOptions.Includes is { Count: > 0 })
        {
            var includeNodes = IncludeTree.SplitDottedPaths(queryOptions.Includes);
            if (includeNodes.Count > 0)
            {
                
                await DapperRowHydrator.HydrateSplitQueryIncludesAsync(
                    rootItems,
                    context,
                    includeNodes);
            }
        }

        IReadOnlyList<object> projected;
        if (queryOptions.Select?.Count > 0 || queryOptions.SelectTree is not null
            || queryOptions.Includes?.Count > 0)
        {
            var selectTree = SelectTreeBuilder.Build(queryOptions);
            projected = rootItems
                .Select(e => DapperResultMaterializer.ProjectEntity(e, selectTree, transformer, typeof(T)))
                .ToList();
        }
        else
        {
            projected = rootItems.Cast<object>().ToList();
        }

        return projected;
    }
}
