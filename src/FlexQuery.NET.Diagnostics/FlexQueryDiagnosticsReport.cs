namespace FlexQuery.NET.Diagnostics;

public sealed class FlexQueryDiagnosticsReport
{
    public Guid QueryId { get; init; }
    public string? Provider { get; init; }
    public string? Translator { get; init; }
    public int? Rows { get; init; }
    public string? Sql { get; init; }
    public Exception? Exception { get; init; }
    public DiagnosticsDuration Duration { get; init; } = new();
    public IReadOnlyList<TimelineEntry> Timeline { get; init; } = [];
}

public sealed class DiagnosticsDuration
{
    public double TotalMs { get; init; }
    public double? ParseMs { get; init; }
    public double? TranslateMs { get; init; }
    public double? DatabaseMs { get; init; }
    public double? MaterializeMs { get; init; }
}

public sealed class TimelineEntry
{
    public string Stage { get; init; } = string.Empty;
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public double DurationMs { get; init; }
}
