namespace FlexQuery.NET.Models;

public readonly record struct QueryParsedEvent(
    Guid QueryId,
    QueryOptions ParsedOptions,
    TimeSpan Duration,
    DateTimeOffset Timestamp);

public readonly record struct QueryTranslatedEvent(
    Guid QueryId,
    string? GeneratedQuery,
    IReadOnlyDictionary<string, object?>? Parameters,
    TimeSpan Duration,
    DateTimeOffset Timestamp);

public readonly record struct QueryExecutedEvent(
    Guid QueryId,
    int? RowCount,
    Exception? Exception,
    TimeSpan Duration,
    DateTimeOffset Timestamp);

public readonly record struct QueryMaterializedEvent(
    Guid QueryId,
    object? Result,
    Exception? Exception,
    TimeSpan Duration,
    DateTimeOffset Timestamp);

/// <summary>
/// Observes read-only execution events during a FlexQuery pipeline run.
/// Implementations receive immutable event records at each stage boundary.
/// Set on <see cref="FlexQueryExecutionConfig.Listener"/> before execution.
/// </summary>
public interface IFlexQueryExecutionListener
{
    /// <summary>Called after <see cref="QueryOptions"/> are parsed from the request.</summary>
    ValueTask QueryParsedAsync(QueryParsedEvent e, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <summary>Called after the query is translated (SQL generated, LINQ built).</summary>
    ValueTask QueryTranslatedAsync(QueryTranslatedEvent e, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Called after the database query executes and raw results are available,
    /// but before results are assembled into the final <c>QueryResult</c>.
    /// </summary>
    ValueTask QueryExecutedAsync(QueryExecutedEvent e, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Called after raw results are materialized into the final <c>QueryResult</c>.
    /// This is the last stage in the pipeline.
    /// </summary>
    ValueTask QueryMaterializedAsync(QueryMaterializedEvent e, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
