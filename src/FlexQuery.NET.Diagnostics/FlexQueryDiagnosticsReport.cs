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