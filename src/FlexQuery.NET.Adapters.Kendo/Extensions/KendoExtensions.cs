using System.Text.Json;
using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Adapters.Kendo.Parsers;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Adapters.Kendo;

/// <summary>
/// Extension methods for Kendo UI integration with FlexQuery.NET.
/// </summary>
public static class KendoExtensions
{
    /// <summary>
    /// Converts a Kendo UI DataSource request to FlexQuery.NET QueryOptions.
    /// </summary>
    /// <param name="request">The Kendo UI DataSource request.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions ToQueryOptions(this KendoRequest request)
    {
        return KendoQueryOptionsParser.Parse(request);
    }

    /// <summary>
    /// Converts a JSON string containing a Kendo UI DataSource request to FlexQuery.NET QueryOptions.
    /// </summary>
    /// <param name="json">The JSON string containing the Kendo UI request.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions ToQueryOptions(this JsonElement json)
    {
        return KendoQueryOptionsParser.Parse(json);
    }

    /// <summary>
    /// Applies Kendo UI DataSource request parameters to an existing QueryOptions instance.
    /// </summary>
    /// <param name="queryOptions">The existing QueryOptions to modify.</param>
    /// <param name="kendoRequest">The Kendo UI DataSource request to apply.</param>
    /// <returns>The modified QueryOptions instance.</returns>
    public static QueryOptions ApplyKendoRequest(this QueryOptions queryOptions, KendoRequest kendoRequest)
    {
        var kendoOptions = KendoQueryOptionsParser.Parse(kendoRequest);
        
        // Apply filters
        if (kendoOptions.Filter != null)
        {
            queryOptions.Filter = kendoOptions.Filter;
        }
        
        // Apply sorts
        if (kendoOptions.Sort.Count > 0)
        {
            queryOptions.Sort = kendoOptions.Sort;
        }
        
        // Apply paging
        if (kendoOptions.Paging.PageSize > 0)
        {
            queryOptions.Paging.PageSize = kendoOptions.Paging.PageSize;
            queryOptions.Paging.Page = kendoOptions.Paging.Page;
        }
        
        // Apply grouping
        if (kendoOptions.GroupBy is { Count: > 0 })
        {
            queryOptions.GroupBy = kendoOptions.GroupBy;
        }
        
        // Apply aggregates
        if (kendoOptions.Aggregates.Count > 0)
        {
            queryOptions.Aggregates = kendoOptions.Aggregates;
        }
        
        return queryOptions;
    }
}
