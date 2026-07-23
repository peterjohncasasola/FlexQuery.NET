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

        var hasNavigation = (queryOptions.Includes?.Count > 0) || (queryOptions.Expand?.Count > 0);
        IReadOnlyList<object> items;

        if (useSimpleIncludeStreaming)
        {
            var result = await SimpleIncludeStreamingMaterializer.MaterializeAsync<T>(
                connection,
                simpleIncludeCommand!,
                mapping,
                options.CommandTimeout,
                ct);

            if (queryOptions.HasProjection())
            {
                Stopwatch.StartNew();
                var selectTree = SelectTreeBuilder.Build(queryOptions);
                items = result.Items
                    .Select(e => DapperResultMaterializer.ProjectEntity(e, selectTree, transformer, typeof(T)))
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                items = result.Items.Cast<object>().ToList();
            }
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
                outer => (Dictionary<string, object>)outer.Value.ToDictionary(
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

        var rootOptions = queryOptions.CopyQueryOptions();
        rootOptions.Includes = null;
        rootOptions.Expand = null;
        rootOptions.Select = null;
        rootOptions.SelectTree = null;
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
            Stopwatch.StartNew();
            foreach (var includePath in queryOptions.Includes)
            {
                ct.ThrowIfCancellationRequested();
                await LoadIncludeViaSplitQueryAsync(
                    connection, rootItems, mapping, registry, dialect, includePath, new SqlTranslator(registry, dialect), ct);
            }
        }

        IReadOnlyList<object> projected;
        if (queryOptions.HasProjection())
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
