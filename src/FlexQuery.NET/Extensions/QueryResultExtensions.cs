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
            Data       = queryResult.Data.Cast<TProjected>().ToList()
        };
    }


    /// <summary>
    /// Awaits a <see cref="QueryResult{TSource}"/> and projects it to <see cref="QueryResult{object}"/>.
    /// </summary>
    public static async Task<QueryResult<object>> ToObjectResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToObjectResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{object}"/>.
    /// </summary>
    public static QueryResult<object> ToObjectResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<object>
        {
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Data       = result.Data?.Cast<object>().ToList() ?? new List<object>()
        };
    }

    /// <summary>
    /// Awaits a <see cref="QueryResult{TSource}"/> and projects it to <see cref="QueryResult{dynamic}"/>.
    /// </summary>
    public static async Task<QueryResult<dynamic>> ToDynamicResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToDynamicResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{dynamic}"/>.
    /// </summary>
    public static QueryResult<dynamic> ToDynamicResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<dynamic>
        {
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Data       = result.Data?.Cast<dynamic>().ToList() ?? new List<dynamic>()
        };
    }
    
}
