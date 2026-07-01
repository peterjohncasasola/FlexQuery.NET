namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Captures the aggregated diagnostic result of a single FlexQuery execution,
/// including provider metadata, generated SQL, row counts, exceptions, durations, and a per-stage timeline.
/// </summary>
public sealed class FlexQueryDiagnosticsReport
{
    /// <summary>
    /// The unique identifier for the query execution.
    /// </summary>
    public Guid QueryId { get; init; }

    /// <summary>
    /// The name of the database provider used (e.g. "SqlServer", "PostgreSQL").
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// The name of the translator used (e.g. "EFCoreTranslator").
    /// </summary>
    public string? Translator { get; init; }

    /// <summary>
    /// The number of rows returned or affected by the query, if available.
    /// </summary>
    public int? Rows { get; init; }

    /// <summary>
    /// The generated SQL string, if translation completed.
    /// </summary>
    public string? Sql { get; init; }

    /// <summary>
    /// The exception that occurred during execution or materialization, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Aggregated duration measurements across all query stages.
    /// </summary>
    public DiagnosticsDuration Duration { get; init; } = new();

    /// <summary>
    /// Ordered list of per-stage timeline entries for this query execution.
    /// </summary>
    public IReadOnlyList<TimelineEntry> Timeline { get; init; } = [];
}