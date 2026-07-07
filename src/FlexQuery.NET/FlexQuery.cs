using FlexQuery.NET.Configurations;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET;

public static class FlexQuery
{
    private static readonly FlexQueryOptions DefaultOptions = new();
    private static readonly IFlexQueryProcessor DefaultProcessor = new FlexQueryProcessor(DefaultOptions);

    public static Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        CancellationToken ct = default)
    {
        return DefaultProcessor.ExecuteAsync(query, options, ct);
    }

    public static Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken ct = default)
    {
        return DefaultProcessor.ExecuteAsync(query, options, execOptions, ct);
    }

    public static Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        FlexQueryExecutionConfig config,
        CancellationToken ct = default)
    {
        return DefaultProcessor.ExecuteAsync(query, options, config, ct);
    }
}
