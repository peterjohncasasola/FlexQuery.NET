using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Collects and retains FlexQuery execution events in memory, providing thread-safe access to
/// recorded events and the ability to build a consolidated <see cref="FlexQueryDiagnosticsReport"/>.
/// Implements <see cref="IFlexQueryExecutionListener"/> to receive query lifecycle notifications.
/// </summary>
public sealed class FlexQueryDiagnosticsCollector : IFlexQueryExecutionListener
{
    private readonly object _lock = new();

    private readonly List<QueryParsedEvent> _parsed = [];
    private readonly List<QueryTranslatedEvent> _translated = [];
    private readonly List<QueryExecutedEvent> _executed = [];
    private readonly List<QueryMaterializedEvent> _materialized = [];

    /// <summary>
    /// Gets a thread-safe snapshot of all recorded <see cref="QueryParsedEvent"/> instances.
    /// </summary>
    public IReadOnlyList<QueryParsedEvent> ParsedEvents
    {
        get { lock (_lock) return _parsed.ToArray(); }
    }

    /// <summary>
    /// Gets a thread-safe snapshot of all recorded <see cref="QueryTranslatedEvent"/> instances.
    /// </summary>
    public IReadOnlyList<QueryTranslatedEvent> TranslatedEvents
    {
        get { lock (_lock) return _translated.ToArray(); }
    }

    /// <summary>
    /// Gets a thread-safe snapshot of all recorded <see cref="QueryExecutedEvent"/> instances.
    /// </summary>
    public IReadOnlyList<QueryExecutedEvent> ExecutedEvents
    {
        get { lock (_lock) return _executed.ToArray(); }
    }

    /// <summary>
    /// Gets a thread-safe snapshot of all recorded <see cref="QueryMaterializedEvent"/> instances.
    /// </summary>
    public IReadOnlyList<QueryMaterializedEvent> MaterializedEvents
    {
        get { lock (_lock) return _materialized.ToArray(); }
    }

    /// <summary>
    /// Records a query-parsed event.
    /// </summary>
    /// <param name="e">The event raised after a query string has been parsed.</param>
    /// <param name="ct">A token used to observe cancellation requests.</param>
    public ValueTask QueryParsedAsync(QueryParsedEvent e, CancellationToken ct)
    {
        lock (_lock) _parsed.Add(e);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Records a query-translated event.
    /// </summary>
    /// <param name="e">The event raised after a parsed query has been translated (e.g. into SQL).</param>
    /// <param name="ct">A token used to observe cancellation requests.</param>
    public ValueTask QueryTranslatedAsync(QueryTranslatedEvent e, CancellationToken ct)
    {
        lock (_lock) _translated.Add(e);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Records a query-executed event.
    /// </summary>
    /// <param name="e">The event raised after a translated query has been executed against the database.</param>
    /// <param name="ct">A token used to observe cancellation requests.</param>
    public ValueTask QueryExecutedAsync(QueryExecutedEvent e, CancellationToken ct)
    {
        lock (_lock) _executed.Add(e);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Records a query-materialized event.
    /// </summary>
    /// <param name="e">The event raised after query results have been materialized into their final form.</param>
    /// <param name="ct">A token used to observe cancellation requests.</param>
    public ValueTask QueryMaterializedAsync(QueryMaterializedEvent e, CancellationToken ct)
    {
        lock (_lock) _materialized.Add(e);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Builds a consolidated diagnostics report from the most recently recorded
    /// parsed, translated, executed, and materialized events.
    /// </summary>
    /// <param name="provider">
    /// An optional label identifying the data provider (e.g. "EFCore", "Dapper") to attach to the report.
    /// </param>
    /// <param name="translator">
    /// An optional label identifying the translator implementation used, to attach to the report.
    /// </param>
    /// <returns>
    /// A <see cref="FlexQueryDiagnosticsReport"/> summarizing the query's identifier, generated SQL,
    /// row count, any exception encountered, total duration, and a per-stage timeline.
    /// </returns>
    public FlexQueryDiagnosticsReport BuildReport(string? provider = null, string? translator = null)
    {
        lock (_lock)
        {
            var lastParsed = _parsed.Count > 0 ? _parsed[^1] : (QueryParsedEvent?)null;
            var lastTranslated = _translated.Count > 0 ? _translated[^1] : (QueryTranslatedEvent?)null;
            var lastExecuted = _executed.Count > 0 ? _executed[^1] : (QueryExecutedEvent?)null;
            var lastMaterialized = _materialized.Count > 0 ? _materialized[^1] : (QueryMaterializedEvent?)null;

            var id = lastMaterialized?.QueryId ?? lastExecuted?.QueryId
                ?? lastTranslated?.QueryId ?? lastParsed?.QueryId ?? Guid.Empty;

            var timeline = BuildTimeline(lastParsed, lastTranslated, lastExecuted, lastMaterialized);
            var duration = BuildDuration(timeline);

            return new FlexQueryDiagnosticsReport
            {
                QueryId = id,
                Provider = provider,
                Translator = translator,
                Sql = lastTranslated?.GeneratedQuery,
                Rows = lastMaterialized is not null ? (lastMaterialized.Value.Result as QueryResult<object>)?.Data?.Count
                    ?? lastExecuted?.RowCount : lastExecuted?.RowCount,
                Exception = lastMaterialized?.Exception ?? lastExecuted?.Exception,
                Duration = duration,
                Timeline = timeline
            };
        }
    }

    /// <summary>
    /// Constructs an ordered list of per-stage timeline entries from the latest events of each kind,
    /// deriving each stage's start time and duration from the cumulative duration recorded by the
    /// previous stage.
    /// </summary>
    /// <param name="parsed">The most recent parse event, if any.</param>
    /// <param name="translated">The most recent translation event, if any.</param>
    /// <param name="executed">The most recent execution event, if any.</param>
    /// <param name="materialized">The most recent materialization event, if any.</param>
    /// <returns>An ordered list of timeline entries, one per stage that has a recorded event.</returns>
    private static List<TimelineEntry> BuildTimeline(
        QueryParsedEvent? parsed,
        QueryTranslatedEvent? translated,
        QueryExecutedEvent? executed,
        QueryMaterializedEvent? materialized)
    {
        var entries = new List<TimelineEntry>(4);

        if (parsed is { } p)
        {
            var start = p.Timestamp - p.Duration;
            entries.Add(new TimelineEntry
            {
                Stage = "Parsing",
                StartUtc = start,
                EndUtc = p.Timestamp,
                DurationMs = p.Duration.TotalMilliseconds
            });
        }

        if (translated is { } t)
        {
            TimeSpan stageDuration;
            if (parsed is { } p2)
                stageDuration = t.Duration - p2.Duration;
            else
                stageDuration = t.Duration;

            var start = t.Timestamp - stageDuration;
            entries.Add(new TimelineEntry
            {
                Stage = "Translation",
                StartUtc = start,
                EndUtc = t.Timestamp,
                DurationMs = stageDuration.TotalMilliseconds
            });
        }

        if (executed is { } e)
        {
            TimeSpan stageDuration;
            if (translated is { } t2)
                stageDuration = e.Duration - t2.Duration;
            else if (parsed is { } p3)
                stageDuration = e.Duration - p3.Duration;
            else
                stageDuration = e.Duration;

            var start = e.Timestamp - stageDuration;
            entries.Add(new TimelineEntry
            {
                Stage = "DatabaseExecution",
                StartUtc = start,
                EndUtc = e.Timestamp,
                DurationMs = stageDuration.TotalMilliseconds
            });
        }

        if (materialized is { } m)
        {
            TimeSpan stageDuration;
            if (executed is { } e2)
                stageDuration = m.Duration - e2.Duration;
            else if (translated is { } t3)
                stageDuration = m.Duration - t3.Duration;
            else if (parsed is { } p4)
                stageDuration = m.Duration - p4.Duration;
            else
                stageDuration = m.Duration;

            var start = m.Timestamp - stageDuration;
            entries.Add(new TimelineEntry
            {
                Stage = "Materialization",
                StartUtc = start,
                EndUtc = m.Timestamp,
                DurationMs = stageDuration.TotalMilliseconds
            });
        }

        return entries;
    }

    /// <summary>
    /// Aggregates a list of timeline entries into a <see cref="DiagnosticsDuration"/> summary,
    /// computing the overall elapsed time plus the individual duration of each named stage.
    /// </summary>
    /// <param name="timeline">The ordered timeline entries produced by <see cref="BuildTimeline"/>.</param>
    /// <returns>
    /// A <see cref="DiagnosticsDuration"/> containing the total elapsed milliseconds (from the start
    /// of the first entry to the end of the last) and the duration of each recognized stage, if present.
    /// </returns>
    private static DiagnosticsDuration BuildDuration(IReadOnlyList<TimelineEntry> timeline)
    {
        var total = timeline.Count > 0
            ? (timeline[^1].EndUtc - timeline[0].StartUtc).TotalMilliseconds
            : 0.0;

        double? GetStage(string name) =>
            timeline.FirstOrDefault(e => e.Stage == name)?.DurationMs;

        return new DiagnosticsDuration
        {
            TotalMs = total,
            ParseMs = GetStage("Parsing"),
            TranslateMs = GetStage("Translation"),
            DatabaseMs = GetStage("DatabaseExecution"),
            MaterializeMs = GetStage("Materialization")
        };
    }

    /// <summary>
    /// Clears all recorded events, discarding the full history for every stage.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _parsed.Clear();
            _translated.Clear();
            _executed.Clear();
            _materialized.Clear();
        }
    }
}