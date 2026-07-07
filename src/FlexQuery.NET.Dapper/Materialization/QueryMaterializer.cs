using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class QueryMaterializer
{
    public static async Task<IReadOnlyList<object>> MaterializeResultsAsync<T>(
        DbConnection connection,
        SqlCommand command,
        DynamicParameters parameters,
        QueryOptions options,
        MappingRegistry registry,
        DapperQueryOptions dapperOptions,
        CancellationToken ct) where T : class
    {
        var mapping = registry.GetMapping(typeof(T));
        var dynamicItems = await connection.QueryAsync(
            command.Sql,
            parameters,
            commandTimeout: dapperOptions.CommandTimeoutSeconds,
            commandType: CommandType.Text);

        var hasIncludes = (options.Includes?.Count > 0 || options.Expand?.Count > 0);
        if (hasIncludes)
        {
            var hydrated = options.Expand?.Count > 0 
                ? DapperRowHydrator.HydrateFilteredIncludes<T>(dynamicItems, mapping, registry, options.Expand) 
                : DapperRowHydrator.HydrateIncludes<T>(dynamicItems, mapping, registry, options.Includes!);

            return hydrated.Cast<object>().ToList();
        }

        if (typeof(T) == typeof(object) || options.GroupBy?.Count > 0 || options.Aggregates.Count > 0)
        {
            return DynamicResultMaterializer.HandleDynamicOrGroupingResults(dynamicItems, options, ct);
        }

        // Fallback: map each dynamic row to a dictionary
        return dynamicItems
            .Select(d => (object)new Dictionary<string, object>((IDictionary<string, object>)d, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
    
}