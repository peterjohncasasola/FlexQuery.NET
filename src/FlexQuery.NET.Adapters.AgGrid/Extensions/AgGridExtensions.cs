using System.Text.Json;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Adapters.AgGrid.Parsers;
using FlexQuery.NET.Adapters.AgGrid.Converters;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Adapters.AgGrid;

/// <summary>
/// Extension methods for AgGrid integration with FlexQuery.NET.
/// </summary>
public static class AgGridExtensions
{
    /// <summary>
    /// Converts an AgGrid request to FlexQuery.NET QueryOptions.
    /// </summary>
    /// <param name="request">The AgGrid request.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions ToQueryOptions(this AgGridRequest request)
    {
        return AgGridQueryOptionsParser.Parse(request);
    }

    /// <summary>
    /// Converts a JSON string containing an AgGrid request to FlexQuery.NET QueryOptions.
    /// </summary>
    /// <param name="json">The JSON string containing the AgGrid request.</param>
    /// <returns>A FlexQuery.NET QueryOptions object.</returns>
    public static QueryOptions ToQueryOptions(this JsonElement json)
    {
        return AgGridQueryOptionsParser.Parse(json);
    }

    /// <summary>
    /// Applies AgGrid request parameters to an existing QueryOptions instance.
    /// </summary>
    /// <param name="queryOptions">The existing QueryOptions to modify.</param>
    /// <param name="agGridRequest">The AgGrid request to apply.</param>
    /// <returns>The modified QueryOptions instance.</returns>
    public static QueryOptions ApplyAgGridRequest(this QueryOptions queryOptions, AgGridRequest agGridRequest)
    {
        var agGridOptions = AgGridQueryOptionsParser.Parse(agGridRequest);
        
        // Apply filters
        if (agGridOptions.Filter != null)
        {
            queryOptions.Filter = agGridOptions.Filter;
        }
        
        // Apply sorts
        if (agGridOptions.Sort.Count > 0)
        {
            queryOptions.Sort = agGridOptions.Sort;
        }
        
        // Apply paging
        if (agGridOptions.Paging.PageSize > 0)
        {
            queryOptions.Paging.PageSize = agGridOptions.Paging.PageSize;
            queryOptions.Paging.Page = agGridOptions.Paging.Page;
        }
        
        // Grouping and aggregates describe the current SSRM store request. They must replace
        // prior values so an expanded leaf request cannot inherit stale grouped-query state.
        queryOptions.GroupBy = agGridOptions.GroupBy;
        queryOptions.Aggregates = agGridOptions.Aggregates;
        
        return queryOptions;
    }

    /// <summary>
    /// Converts a FlexQuery result into an AG Grid SSRM response payload
    /// with the default property-name casing (PascalCase from CLR types).
    /// </summary>
    public static AgGridServerSideResponse ToAgGridServerSideResponse<T>(
        this QueryResult<T> result,
        AgGridRequest request,
        AgGridResponseFieldOptions? options = null)
    {
        return AgGridResponseConverter.Convert(request, result, false, options);
    }

    /// <summary>
    /// Converts a FlexQuery result into an AG Grid SSRM response payload.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="camelCase">
    /// When <c>true</c>, property names in row data dictionaries are converted to camelCase.
    /// Use this when your AG Grid column definitions use camelCase field names.
    /// </param>
    /// <param name="result"></param>
    /// <param name="options"></param>
    public static AgGridServerSideResponse ToAgGridServerSideResponse<T>(
        this QueryResult<T> result,
        AgGridRequest request,
        bool camelCase,
        AgGridResponseFieldOptions? options = null)
    {
        return AgGridResponseConverter.Convert(request, result, options: options, camelCase: camelCase);
    }
}
