namespace FlexQuery.NET.Models;

/// <summary>
/// Event data raised after query options have been successfully parsed from the request.
/// Contains the parsed options and timing information for diagnostics.
/// </summary>
/// <param name="QueryId">A unique identifier for the query execution.</param>
/// <param name="ParsedOptions">The parsed query options resulting from the parsing phase.</param>
/// <param name="Duration">The time elapsed during the parsing phase.</param>
/// <param name="Timestamp">The UTC timestamp when the parsing completed.</param>
public readonly record struct QueryParsedEvent(
    Guid QueryId,
    QueryOptions ParsedOptions,
    TimeSpan Duration,
    DateTimeOffset Timestamp);
