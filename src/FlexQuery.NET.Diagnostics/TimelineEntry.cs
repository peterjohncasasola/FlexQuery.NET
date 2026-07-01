namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Represents a single stage within the FlexQuery execution timeline,
/// recording the stage name, start and end timestamps, and elapsed duration.
/// </summary>
public sealed class TimelineEntry
{
    /// <summary>
    /// The name of the stage (e.g. "Parsing", "Translation", "DatabaseExecution", "Materialization").
    /// </summary>
    public string Stage { get; init; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when the stage started.
    /// </summary>
    public DateTimeOffset StartUtc { get; init; }

    /// <summary>
    /// The UTC timestamp when the stage completed.
    /// </summary>
    public DateTimeOffset EndUtc { get; init; }

    /// <summary>
    /// The duration of this stage in milliseconds.
    /// </summary>
    public double DurationMs { get; init; }
}