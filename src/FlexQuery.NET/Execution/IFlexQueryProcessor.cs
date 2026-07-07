using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Execution;

public interface IFlexQueryProcessor
{
    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        CancellationToken ct = default);

    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken ct = default);

    Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        FlexQueryExecutionConfig config,
        CancellationToken ct = default);
}
