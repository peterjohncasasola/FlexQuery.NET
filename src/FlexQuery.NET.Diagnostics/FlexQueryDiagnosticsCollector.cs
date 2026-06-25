using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

public sealed class FlexQueryDiagnosticsCollector : IFlexQueryExecutionListener
{
    private readonly object _lock = new();

    private readonly List<QueryParsedEvent> _parsed = [];
    private readonly List<QueryTranslatedEvent> _translated = [];
    private readonly List<QueryExecutedEvent> _executed = [];
    private readonly List<QueryMaterializedEvent> _materialized = [];

    public IReadOnlyList<QueryParsedEvent> ParsedEvents
    {
        get { lock (_lock) return _parsed.ToArray(); }
    }

    public IReadOnlyList<QueryTranslatedEvent> TranslatedEvents
    {
        get { lock (_lock) return _translated.ToArray(); }
    }

    public IReadOnlyList<QueryExecutedEvent> ExecutedEvents
    {
        get { lock (_lock) return _executed.ToArray(); }
    }

    public IReadOnlyList<QueryMaterializedEvent> MaterializedEvents
    {
        get { lock (_lock) return _materialized.ToArray(); }
    }

    public ValueTask QueryParsedAsync(QueryParsedEvent e, CancellationToken ct)
    {
        lock (_lock) _parsed.Add(e);
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryTranslatedAsync(QueryTranslatedEvent e, CancellationToken ct)
    {
        lock (_lock) _translated.Add(e);
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryExecutedAsync(QueryExecutedEvent e, CancellationToken ct)
    {
        lock (_lock) _executed.Add(e);
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryMaterializedAsync(QueryMaterializedEvent e, CancellationToken ct)
    {
        lock (_lock) _materialized.Add(e);
        return ValueTask.CompletedTask;
    }

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
