using FlexQuery.NET.Models;

namespace FlexQuery.NET;

/// <summary>
/// Extensions for <see cref="QueryResult{T}"/>.
/// </summary>
public static class QueryResultExtensions
{
    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{TProjected}"/> by casting data items.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TProjected">The target item type.</typeparam>
    /// <param name="queryResult">The query result to project.</param>
    /// <returns>A new <see cref="QueryResult{TProjected}"/> with the same metadata and cast data.</returns>
    public static QueryResult<TProjected> ToProjectedQueryResult<TSource, TProjected>(
        this QueryResult<TSource> queryResult)
    {
        return new QueryResult<TProjected>
        {
            TotalCount = queryResult.TotalCount,
            ResultCount = queryResult.ResultCount,
            Page       = queryResult.Page,
            PageSize   = queryResult.PageSize,
            Aggregates = queryResult.Aggregates,
            Data       = queryResult.Data.Cast<TProjected>().ToList(),
            NextCursorToken = queryResult.NextCursorToken
        };
    }


    /// <summary>
    /// Awaits a <see cref="QueryResult{T}"/> and projects it to a <c>QueryResult{object}</c>.
    /// </summary>
    /// <typeparam name="T">The source item type.</typeparam>
    /// <param name="result">A task returning the query result to convert.</param>
    /// <returns>A task representing the conversion to a <c>QueryResult{object}</c>.</returns>
    public static async Task<QueryResult<object>> ToObjectResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToObjectResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{T}"/> to a <c>QueryResult{object}</c>.
    /// </summary>
    /// <typeparam name="T">The source item type.</typeparam>
    /// <param name="result">The query result to convert.</param>
    /// <returns>A <c>QueryResult{object}</c> with the same metadata and cast data.</returns>
    public static QueryResult<object> ToObjectResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<object>
        {
            TotalCount = result.TotalCount,
            ResultCount = result.ResultCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Aggregates = result.Aggregates,
            Data       = result.Data?.Cast<object>().ToList() ?? new List<object>(),
            NextCursorToken = result.NextCursorToken
        };
    }

    /// <summary>
    /// Awaits a <see cref="QueryResult{TSource}"/> and projects it to <see cref="QueryResult{T}"/> with dynamic items.
    /// </summary>
    /// <typeparam name="T">The source item type.</typeparam>
    /// <param name="result">A task returning the query result to convert.</param>
    /// <returns>A task representing the conversion to <see cref="QueryResult{dynamic}"/>.</returns>
    public static async Task<QueryResult<dynamic>> ToDynamicResultAsync<T>(this Task<QueryResult<T>> result)
    {
        var queryResult = await result.ConfigureAwait(false);
        return queryResult.ToDynamicResult();
    }

    /// <summary>
    /// Projects a <see cref="QueryResult{TSource}"/> to a <see cref="QueryResult{T}"/> with dynamic items.
    /// </summary>
    /// <typeparam name="T">The source item type.</typeparam>
    /// <param name="result">The query result to convert.</param>
    /// <returns>A <see cref="QueryResult{dynamic}"/> with the same metadata and cast data.</returns>
    public static QueryResult<dynamic> ToDynamicResult<T>(this QueryResult<T> result)
    {
        return new QueryResult<dynamic>
        {
            TotalCount = result.TotalCount,
            ResultCount = result.ResultCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
            Aggregates = result.Aggregates,
            Data       = result.Data?.Cast<dynamic>().ToList() ?? new List<dynamic>(),
            NextCursorToken = result.NextCursorToken
        };
    }
    
}

