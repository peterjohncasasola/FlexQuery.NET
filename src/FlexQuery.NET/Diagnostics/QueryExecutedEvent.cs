using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Event data raised after the database query executes and raw results are available,
/// but before results are assembled into the final <see cref="QueryResult{T}"/>.
/// </summary>
/// <param name="QueryId">A unique identifier for the query execution.</param>
/// <param name="RowCount">The number of rows returned by the query, if available.</param>
/// <param name="Exception">The exception that occurred during execution, if any.</param>
/// <param name="Duration">The time elapsed during the execution phase.</param>
/// <param name="Timestamp">The UTC timestamp when the execution completed.</param>
public readonly record struct QueryExecutedEvent(
    Guid QueryId,
    int? RowCount,
    Exception? Exception,
    TimeSpan Duration,
    DateTimeOffset Timestamp);
