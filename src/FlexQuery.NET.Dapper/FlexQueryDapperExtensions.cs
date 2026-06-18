using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Extensions;
using Microsoft.Extensions.Primitives;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Extension methods for executing FlexQuery requests with Dapper.
/// </summary>
public static class FlexQueryDapperExtensions
{
    /// <summary>
    /// Executes a FlexQuery using FlexQueryParameters with validation.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        Action<DapperQueryOptions>? configureDapper = null) where T : class
    {
        var dapperOptions = new DapperQueryOptions();
        configureDapper?.Invoke(dapperOptions);

        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? typeof(T);

        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions);
    }

    /// <summary>
    /// Executes a FlexQuery using FlexQueryParameters with full options.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        FlexQueryParameters parameters,
        DapperQueryOptions? dapperQueryOptions = null) where T : class
    {
        var dapperOptions = dapperQueryOptions ?? new DapperQueryOptions();
        var parsedOptions = QueryOptionsParser.Parse(parameters);
        parsedOptions.Items[ContextKeys.EntityType] = dapperOptions.EntityType ?? typeof(T);
       
        var execOptions = dapperOptions.ToQueryExecutionOptions();

        parsedOptions.ValidateOrThrow(dapperOptions.EntityType ?? typeof(T), execOptions);

        return await ExecuteQueryAsync<T>(connection, parsedOptions, dapperOptions);
    }

    /// <summary>
    /// Executes a FlexQuery using raw query string parameters.
    /// </summary>
    public static async Task<QueryResult<T>> FlexQueryAsync<T>(
        this DbConnection connection,
        IDictionary<string, StringValues> parameters,
        Action<DapperQueryOptions>? configureDapper = null) where T : class
    {
        var dict = parameters.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        
        var flexParams = new FlexQueryParameters
        {
            Filter = dict.GetValueOrDefault("filter") ?? dict.GetValueOrDefault("$filter"),
            Sort = dict.GetValueOrDefault("sort") ?? dict.GetValueOrDefault("orderby") ?? dict.GetValueOrDefault("$orderby"),
            Select = dict.GetValueOrDefault("select") ?? dict.GetValueOrDefault("$select"),
            Include = dict.GetValueOrDefault("include") ?? dict.GetValueOrDefault("expand") ?? dict.GetValueOrDefault("$expand"),
            Page = dict.TryGetValue("page", out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = dict.TryGetValue("pageSize", out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null,
            RawParameters = dict
        };

        return await FlexQueryAsync<T>(connection, flexParams, configureDapper);
    }

    private static async Task<QueryResult<T>> ExecuteQueryAsync<T>(
        DbConnection connection,
        QueryOptions options,
        DapperQueryOptions execOptions) where T : class
    {
        var dialect = execOptions.Dialect 
            ?? DapperQueryOptions.GlobalDefaultDialect 
            ?? DapperQueryOptions.GlobalDialectResolver.Resolve(connection);
        
        var registry = execOptions.MappingRegistry ?? new Mapping.MappingRegistry();
        
        // Propagate EntityType to options for translator
        if (execOptions.EntityType != null)
            options.Items[ContextKeys.EntityType] = execOptions.EntityType;

        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);
        var mapping = registry.GetMapping(execOptions.EntityType ?? typeof(T));

        var parameters = new DynamicParameters();
        foreach (var param in command.Parameters)
        {
            var cleanName = param.Key.TrimStart('@', ':', '?');
            parameters.Add(cleanName, param.Value);
        }

        IReadOnlyList<T> items;
        if (options.Includes?.Count > 0)
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);

            var parentMap = new Dictionary<object, T>();
            var pkProperty = mapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase)) ?? mapping.GetProperties().First();
            var pkColumn = mapping.GetColumnName(pkProperty);

            foreach (var row in dynamicItems)
            {
                var rowDict = (IDictionary<string, object>)row;
                System.IO.File.AppendAllText("hydration_log.txt", $"ROW KEYS: {string.Join(", ", rowDict.Keys)}\n");
                var rowKeys = rowDict.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
                
                if (!rowKeys.TryGetValue(pkColumn, out var actualPkCol) || rowDict[actualPkCol] == null || rowDict[actualPkCol] == DBNull.Value) continue;
                var pkValue = rowDict[actualPkCol];

                if (!parentMap.TryGetValue(pkValue, out var parent))
                {
                    parent = MapRowToEntity<T>(rowDict, mapping, string.Empty);
                    parentMap[pkValue] = parent;
                }

                foreach (var include in options.Includes)
                {
                    var joinInfo = mapping.GetJoinInfo(include);
                    if (joinInfo == null) continue;

                    var targetMapping = registry.GetMapping(joinInfo.TargetType);
                    var childPkProperty = targetMapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase)) ?? targetMapping.GetProperties().First();
                    var childPkColumn = joinInfo.NavigationProperty + "_" + targetMapping.GetColumnName(childPkProperty);

                    if (rowKeys.TryGetValue(childPkColumn, out var actualChildPkCol) && rowDict[actualChildPkCol] != null && rowDict[actualChildPkCol] != DBNull.Value)
                    {
                        var child = MapRowToEntity(rowDict, targetMapping, joinInfo.NavigationProperty + "_");
                        AddChildToParent(parent, joinInfo.NavigationProperty, child);
                    }
                }
            }
            items = parentMap.Values.ToList();
        }
        else if (typeof(T) == typeof(object) || options.Aggregates.Count > 0)
        {
            var dynamicItems = await connection.QueryAsync(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text);
            
            items = dynamicItems
                .Select(d => (T)(object)new Dictionary<string, object>((IDictionary<string, object>)d, StringComparer.OrdinalIgnoreCase))
                .AsList();
        }
        else
        {
            items = (await connection.QueryAsync<T>(
                command.Sql,
                parameters,
                commandTimeout: execOptions.CommandTimeoutSeconds,
                commandType: CommandType.Text)).AsList();
        }

        var totalCount = items.Count;
        if (execOptions.IncludeTotalCount && (options.Paging.Page > 1 || (options.Paging.PageSize > 0 && items.Count == options.Paging.PageSize)))
        {
            var countSql = ExtractCountSql(command.Sql);
            totalCount = (int)await connection.QuerySingleAsync<long>(countSql, parameters, commandTimeout: execOptions.CommandTimeoutSeconds, commandType: CommandType.Text);
        }

        return new QueryResult<T>
        {
            Data = items,
            TotalCount = totalCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize
        };
    }

    private static T MapRowToEntity<T>(IDictionary<string, object> row, Mapping.IEntityMapping mapping, string prefix) where T : class
    {
        return (T)MapRowToEntity(row, mapping, prefix);
    }

    private static object MapRowToEntity(IDictionary<string, object> row, Mapping.IEntityMapping mapping, string prefix)
    {
        var entity = Activator.CreateInstance(mapping.Type)!;
        var rowKeys = row.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var propName in mapping.GetProperties())
        {
            var colName = prefix + mapping.GetColumnName(propName);
            if (rowKeys.TryGetValue(colName, out var actualKey) && row.TryGetValue(actualKey, out var val) && val != DBNull.Value)
            {
                var prop = mapping.Type.GetProperty(propName);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        prop.SetValue(entity, Convert.ChangeType(val, targetType));
                    }
                    catch { /* skip incompatible */ }
                }
            }
        }
        return entity;
    }

    private static void AddChildToParent(object parent, string navigationProperty, object child)
    {
        var prop = parent.GetType().GetProperty(navigationProperty, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null) return;

        var value = prop.GetValue(parent);
        if (value == null)
        {
            var propType = prop.PropertyType;
            if (propType.IsGenericType && (propType.GetGenericTypeDefinition() == typeof(List<>) || propType.GetGenericTypeDefinition() == typeof(ICollection<>) || propType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var itemType = propType.GetGenericArguments()[0];
                value = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                prop.SetValue(parent, value);
            }
            else
            {
                prop.SetValue(parent, child);
                return;
            }
        }

        if (value is System.Collections.IList list)
        {
            var childPkProp = child.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
            var childPk = childPkProp?.GetValue(child);
            
            if (childPk != null)
            {
                foreach (var item in list)
                {
                    var itemPk = item.GetType().GetProperty(childPkProp.Name)?.GetValue(item);
                    if (childPk.Equals(itemPk))
                        return;
                }
            }
            list.Add(child);
        }
    }

    private static string ExtractCountSql(string sql)
    {
        var keywords = new[] { "ORDER BY", "LIMIT", "OFFSET" };
        var minIdx = sql.Length;
        foreach (var kw in keywords)
        {
            var idx = sql.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < minIdx) minIdx = idx;
        }
        var baseSql = sql[..minIdx];
        return $"SELECT COUNT(1) FROM ({baseSql.Trim()}) AS CountTable";
    }
}
