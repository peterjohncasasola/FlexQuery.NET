using FlexQuery.NET.Execution;

namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// A diagnostics listener that writes query execution events to the console.
/// Useful for local debugging and development-time diagnostics.
/// </summary>
public sealed class ConsoleExecutionListener : IFlexQueryExecutionListener
{
    /// <summary>
    /// Writes a parsed options summary to the console.
    /// </summary>
    public ValueTask QueryParsedAsync(QueryParsedEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[FlexQuery] [{e.QueryId}] Parsed in {e.Duration.TotalMilliseconds:F1}ms: {e.ParsedOptions}");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes the translated SQL and parameter list to the console.
    /// </summary>
    public ValueTask QueryTranslatedAsync(QueryTranslatedEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[FlexQuery] [{e.QueryId}] Translated in {e.Duration.TotalMilliseconds:F1}ms");
        if (e.GeneratedQuery is not null)
            Console.WriteLine($"  SQL: {e.GeneratedQuery}");
        if (e.Parameters is { Count: > 0 })
        {
            Console.WriteLine("  Parameters:");
            foreach (var p in e.Parameters)
                Console.WriteLine($"    {p.Name} = {p.Value}");
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes database execution results (row count or exception) to the console.
    /// </summary>
    public ValueTask QueryExecutedAsync(QueryExecutedEvent e, CancellationToken ct)
    {
        Console.WriteLine(e.Exception is not null
            ? $"[FlexQuery] [{e.QueryId}] Database failed in {e.Duration.TotalMilliseconds:F1}ms: {e.Exception.Message}"
            : $"[FlexQuery] [{e.QueryId}] Database executed in {e.Duration.TotalMilliseconds:F1}ms, rows={e.RowCount}");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes materialization results (success or exception) to the console.
    /// </summary>
    public ValueTask QueryMaterializedAsync(QueryMaterializedEvent e, CancellationToken ct)
    {
        Console.WriteLine(e.Exception is not null
            ? $"[FlexQuery] [{e.QueryId}] Materialization failed in {e.Duration.TotalMilliseconds:F1}ms: {e.Exception.Message}"
            : $"[FlexQuery] [{e.QueryId}] Materialized in {e.Duration.TotalMilliseconds:F1}ms");
        return ValueTask.CompletedTask;
    }
}
