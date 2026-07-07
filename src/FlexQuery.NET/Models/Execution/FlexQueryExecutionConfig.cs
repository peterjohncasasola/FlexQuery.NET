namespace FlexQuery.NET.Models;

/// <summary>
/// Per-execution configuration for a single FlexQuery call.
/// This object is created for each execution and is NOT shared or reused.
/// Set <see cref="Listener"/> to observe query execution events (diagnostics, logging, tracing).
/// </summary>
public sealed class FlexQueryExecutionConfig
{
    /// <summary>
    /// Optional listener that receives read-only execution events.
    /// The listener is called synchronously within the query pipeline.
    /// Slow listeners will delay query execution.
    /// </summary>
    public IFlexQueryExecutionListener? Listener { get; set; }
}
