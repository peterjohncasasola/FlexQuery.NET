namespace FlexQuery.NET.Diagnostics;

public sealed class TimelineEntry
{
    public string Stage { get; init; } = string.Empty;
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public double DurationMs { get; init; }
}