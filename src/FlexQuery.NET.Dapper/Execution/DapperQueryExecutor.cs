using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Dapper;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Dapper.Configuration;
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

        return await ExecuteAsync<T>(connection, queryOptions, options, ctx);
    }

    public static async Task<QueryResult<TResponse>> RunDtoAsync<TEntity, TResponse>(
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        IQuerySurface surface,
        IReadOnlyList<Models.SelectOutputField>? resultShape = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TResponse : class
    {
        var ctx = options.Listener is not null
            ? new FlexQueryExecutionContext(options.Listener, cancellationToken)
            : null;

        queryOptions.Items[ContextKeys.EntityType] = typeof(TEntity);

        await ctx.NotifyParsedAsync(queryOptions);

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
        // QueryResult.Aggregates — never into the DTO row shape.
        var isGrouped = queryOptions.GroupBy is { Count: > 0 };

        var command = translator.Translate(BuildRootOnlyOptions(queryOptions));
        var parameters = CommandParameterAdapter.ToDynamicParameters(command);

        if (ctx is not null)
        {
            var queryParameters =
                command.Parameters.Select(p => new QueryParameter(p.Key, p.Value)).ToList().AsReadOnly();
            await ctx.NotifyTranslatedAsync(command.Sql, queryParameters);
        }

        var rows = await connection.QueryAsync(
            command.Sql,
            parameters!,
            commandTimeout: options.CommandTimeout,
            commandType: CommandType.Text);

        var rowsList = rows.ToList();

        IReadOnlyList<TResponse> items;
        if (isGrouped)
        {
            // Grouped queries have their own result shape (group keys + aliases): project
            // into TResponse when the DTO models that shape, otherwise fall back to the
            // dynamic grouped flow — aggregate aliases are result metadata, not row
            // properties, and must not be required on the DTO.
            if (!CanRepresentGroupedShape<TResponse>(queryOptions, surface))
            {
                var dynamicResult = await ExecuteAsync<TEntity>(connection, queryOptions, options, ctx);
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

        var (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(connection, queryOptions, translator, command, parameters, options);

        // Grand totals ride the existing aggregate metadata channel (QueryResult.Aggregates).
        // Aggregate metadata keys use the PUBLIC field identity (DTO names) in DTO mode.
        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(
            connection, queryOptions, translator, options, ct,
            fieldName => surface.TryResolveByEntityName(fieldName, out var resolved)
                ? resolved.SurfaceName
                : fieldName);

        await ctx.NotifyExecutedAsync(items.Count);

        var effectiveShape = isGrouped
            ? (resultShape ?? ResultShapeBuilder.BuildGroupedShape(queryOptions))
            : resultShape;

        var queryResult = queryOptions.BuildQueryResult(data: items, totalCount, aggregates: grandTotals, resultCount);
        queryResult.ResultShape = effectiveShape;

        await ctx.NotifyMaterializedAsync(queryResult);

        return queryResult;
    }

    /// <summary>
    /// Returns true when the response DTO models the full grouped result shape —
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
            if (!writableProps.ContainsKey(Builders.GroupByBuilder.GetProjectionName(publicName)))
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
        DbConnection connection,
        QueryOptions queryOptions,
        DapperQueryOptions options,
        FlexQueryExecutionContext? ctx)
        where T : class
    {
        var ct = ctx?.CancellationToken ?? CancellationToken.None;

        await ConnectionHelper.EnsureOpenAsync(connection, ct);

        var dialect = SqlDialectResolver.Resolve(connection);
        var registry = options.Model?.Registry
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
        // root SQL — includes exist only to satisfy the navigation-include authorization
        // contract and must not trigger navigation hydration/split queries.
        var isFlatProjection =
            (queryOptions.ProjectionMode == ProjectionMode.Flat
             || queryOptions.ProjectionMode == ProjectionMode.FlatMixed)
            && queryOptions.HasProjection();

        var hasNavigation = !isFlatProjection &&
                            ((queryOptions.Includes?.Count > 0) || (queryOptions.Expand?.Count > 0));
        IReadOnlyList<object> items;

        if (useSimpleIncludeStreaming)
        {
            var selectTree = SelectTreeBuilder.Build(queryOptions);
            var projectedResult = await SimpleIncludeStreamingMaterializer.MaterializeProjectedAsync(
                connection,
                simpleIncludeCommand!,
                mapping,
                options.CommandTimeout,
                selectTree,
                ct);

            items = projectedResult.Items;
        }
        else
        {
                var rows = await connection.QueryAsync(
                command.Sql,
                parameters!,
                commandTimeout: options.CommandTimeout,
                commandType: CommandType.Text);

            var rowsList = rows.ToList();

            if (hasNavigation)
            {
                items = await ExecuteSplitQueryAsync<T>(
                    connection,
                    queryOptions,
                    mapping,
                    registry,
                    dialect,
                    rowsList,
                    command.ColumnAliasMap,
                    transformer,
                    ct);
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

        var (totalCount, resultCount) = await CountEvaluator.GetCountsAsync(connection, queryOptions, translator, command, parameters, options);

        var grandTotals = await AggregateEvaluator.GetGrandTotalsAsync(connection, queryOptions, translator, options, ct);

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
        if ((queryOptions.Includes?.Count ?? 0) == 0
            && (queryOptions.Expand?.Count ?? 0) == 0)
        {
            return queryOptions;
        }

        // Flat projection modes deliver leaf columns through the single-query JOIN — the
        // select shape (including navigation leaf paths) must survive, otherwise the root
        // SQL degrades to a full root-column scan and the flat output is lost.
        var isFlatProjection =
            (queryOptions.ProjectionMode == ProjectionMode.Flat
             || queryOptions.ProjectionMode == ProjectionMode.FlatMixed)
            && queryOptions.HasProjection();

        var rootOptions = queryOptions.CopyQueryOptions();
        rootOptions.Includes = null;
        rootOptions.Expand = null;
        if (!isFlatProjection)
        {
            rootOptions.Select = null;
            rootOptions.SelectTree = null;
        }
        return rootOptions;
    }

    private static async Task<IReadOnlyList<object>> ExecuteSplitQueryAsync<T>(
        DbConnection connection,
        QueryOptions queryOptions,
        IEntityMapping mapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        IReadOnlyList<dynamic> rowsList,
        Dictionary<string, string>? columnAliasMap,
        Func<string, string>? transformer,
        CancellationToken ct)
        where T : class
    {
        var rootItems = DapperRowHydrator.HydrateCore<T>(
            rowsList, mapping, registry, new List<string> { string.Empty }, columnAliasMap);

        if (rootItems.Count == 0)
            return Array.Empty<object>();

        var expandNodes = queryOptions.Expand ?? new List<IncludeNode>();

        foreach (var expandNode in expandNodes)
            ct.ThrowIfCancellationRequested();

        if (expandNodes.Count > 0)
        {
            await DapperRowHydrator.HydrateSplitQueryIncludesAsync(
                rootItems,
                mapping,
                registry,
                dialect,
                connection,
                expandNodes,
                new SqlParameterContext(dialect),
                new SqlTranslator(registry, dialect),
                ct);

            if (queryOptions.Includes is { Count: > 0 })
            {
                var expandedRootPaths = new HashSet<string>(
                    expandNodes.Select(node => node.Path),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var includePath in queryOptions.Includes.Where(path => !expandedRootPaths.Contains(path)))
                {
                    ct.ThrowIfCancellationRequested();
                    await DapperRowHydrator.LoadNavigationAsync(
                        rootItems.Cast<object>().ToList(),
                        mapping,
                        registry,
                        dialect,
                        connection,
                        includePath,
                        new SqlParameterContext(dialect),
                        new SqlTranslator(registry, dialect),
                        expandNode: null,
                        ct);
                }
            }
        }
        else if (queryOptions.Includes is { Count: > 0 })
        {
            foreach (var includePath in queryOptions.Includes)
            {
                ct.ThrowIfCancellationRequested();
                await LoadIncludeViaSplitQueryAsync(
                    connection, rootItems, mapping, registry, dialect, includePath, new SqlTranslator(registry, dialect), ct);
            }
        }

        IReadOnlyList<object> projected;
        if (queryOptions.Select?.Count > 0 || queryOptions.SelectTree is not null
            || queryOptions.Includes?.Count > 0 || queryOptions.Expand?.Count > 0)
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

    private static async Task LoadIncludeViaSplitQueryAsync<T>(
        DbConnection connection,
        IReadOnlyList<T> roots,
        IEntityMapping rootMapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        string navigationPath,
        SqlTranslator sqlTranslator,
        CancellationToken ct)
        where T : class
    {
        var rel = rootMapping.GetRelationship(navigationPath);
        if (rel?.TargetType == null) return;

        var pkProperty = rootMapping.GetKeyProperties().FirstOrDefault()
            ?? rootMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? rootMapping.GetProperties().First();

        var rootPks = new List<object?>();
        foreach (var root in roots)
        {
            var pkVal = root.GetType().GetProperty(pkProperty)?.GetValue(root);
            if (pkVal != null) rootPks.Add(pkVal);
        }

        if (rootPks.Count == 0) return;

        var parameters = new SqlParameterContext(dialect);
        var sql = sqlTranslator.BuildIncludeSql(
            navigationPath,
            rootMapping,
            registry.GetMapping(rel.TargetType),
            parameters,
            rootPks);

        if (string.IsNullOrEmpty(sql)) return;

        var rows = await connection.QueryAsync(
            sql,
            parameters.RawParameters,
            commandTimeout: null,
            commandType: CommandType.Text);

        var rowList = rows.ToList();
        if (!rowList.Any()) return;

        var targetMapping = registry.GetMapping(rel.TargetType);
        var navPrefix = navigationPath + "_";
        var hydrated = DapperRowHydrator.HydrateCoreNonGeneric(
            rowList, targetMapping, registry, new List<string> { string.Empty }, null, navPrefix).ToList();

        var rootPkPropInfo = roots[0].GetType().GetProperty(pkProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (rootPkPropInfo == null) return;

        var rootIndex = roots.ToDictionary(rootPkPropInfo.GetValue, r => r);

        foreach (var child in hydrated)
        {
            var childType = child.GetType();
            var childFkProp = childType.GetProperty(rel.ForeignKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (childFkProp == null) continue;

            var childFk = childFkProp.GetValue(child);
            if (childFk == null) continue;

            if (rootIndex.TryGetValue(childFk, out var root))
                DapperRowHydrator.AddChildToParent(root, navigationPath, child);
        }
    }
}
