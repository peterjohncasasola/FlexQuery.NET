using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Event data raised after raw query results have been materialized into the final
/// <see cref="QueryResult{T}"/>. Contains the result, any exception that occurred,
/// and timing information.
/// </summary>
/// <param name="QueryId">A unique identifier for the query execution.</param>
/// <param name="Result">The materialized result object, if successful.</param>
/// <param name="Exception">The exception that occurred during materialization, if any.</param>
/// <param name="Duration">The time elapsed during the materialization phase.</param>
/// <param name="Timestamp">The UTC timestamp when the materialization completed.</param>
public readonly record struct QueryMaterializedEvent(
    Guid QueryId,
    object? Result,
    Exception? Exception,
    TimeSpan Duration,
    DateTimeOffset Timestamp);
