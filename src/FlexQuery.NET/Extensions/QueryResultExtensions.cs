using FlexQuery.NET.Models;

namespace FlexQuery.NET.Extensions;

/// <summary>
/// Extensions for <see cref="QueryResult{T}"/>.
/// </summary>
public static class QueryResultExtensions
{
    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{TProjected}"/> by casting data items.
    /// </summary>
    public static QueryResult<TProjected> ToProjectedQueryResult<TSource, TProjected>(
        this QueryResult<TSource> queryResult)
    {
        return new QueryResult<TProjected>
        {
            TotalCount = queryResult.TotalCount,
            Page       = queryResult.Page,
            PageSize   = queryResult.PageSize,
            Aggregates = queryResult.Aggregates,
            Data       = queryResult.Data.Cast<TProjected>().ToList()
        };
    }


    /// <summary>
    /// Awaits a <see cref="QueryResult{TSource}"/> and projects it to <see cref="QueryResult{T}"/> with object items.
    /// </summary>
    public static async Task<QueryResult<object>> ToObjectResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToObjectResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{T}"/> with object items.
    /// </summary>
    public static QueryResult<object> ToObjectResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<object>
        {
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Aggregates = result.Aggregates,
            Data       = result.Data?.Cast<object>().ToList() ?? new List<object>()
        };
    }

    /// <summary>
    /// Awaits a <see cref="QueryResult{TSource}"/> and projects it to <see cref="QueryResult{T}"/> with dynamic items.
    /// </summary>
    public static async Task<QueryResult<dynamic>> ToDynamicResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToDynamicResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{T}"/> with dynamic items.
    /// </summary>
    public static QueryResult<dynamic> ToDynamicResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<dynamic>
        {
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Aggregates = result.Aggregates,
            Data       = result.Data?.Cast<dynamic>().ToList() ?? new List<dynamic>()
        };
    }
    
}
