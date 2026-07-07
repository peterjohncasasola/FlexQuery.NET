using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Event data raised after the query has been translated into the underlying query representation
/// (SQL, LINQ expression, etc.). Contains the generated query text, parameters, and timing information.
/// </summary>
/// <param name="QueryId">A unique identifier for the query execution.</param>
/// <param name="GeneratedQuery">The generated query text (e.g., SQL string), if available.</param>
/// <param name="Parameters">The parameters used in the generated query, if available.</param>
/// <param name="Duration">The time elapsed during the translation phase.</param>
/// <param name="Timestamp">The UTC timestamp when the translation completed.</param>
public readonly record struct QueryTranslatedEvent(
    Guid QueryId,
    string? GeneratedQuery,
    IReadOnlyList<QueryParameter>? Parameters,
    TimeSpan Duration,
    DateTimeOffset Timestamp);
