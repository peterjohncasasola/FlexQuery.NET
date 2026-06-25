using FlexQuery.NET.Models;

namespace FlexQuery.NET.Diagnostics;

public sealed class ConsoleExecutionListener : IFlexQueryExecutionListener
{
    public ValueTask QueryParsedAsync(QueryParsedEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[FlexQuery] [{e.QueryId}] Parsed in {e.Duration.TotalMilliseconds:F1}ms: {e.ParsedOptions}");
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryTranslatedAsync(QueryTranslatedEvent e, CancellationToken ct)
    {
        Console.WriteLine($"[FlexQuery] [{e.QueryId}] Translated in {e.Duration.TotalMilliseconds:F1}ms");
        if (e.GeneratedQuery is not null)
            Console.WriteLine($"  SQL: {e.GeneratedQuery}");
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryExecutedAsync(QueryExecutedEvent e, CancellationToken ct)
    {
        if (e.Exception is not null)
            Console.WriteLine($"[FlexQuery] [{e.QueryId}] Database failed in {e.Duration.TotalMilliseconds:F1}ms: {e.Exception.Message}");
        else
            Console.WriteLine($"[FlexQuery] [{e.QueryId}] Database executed in {e.Duration.TotalMilliseconds:F1}ms, rows={e.RowCount}");
        return ValueTask.CompletedTask;
    }

    public ValueTask QueryMaterializedAsync(QueryMaterializedEvent e, CancellationToken ct)
    {
        if (e.Exception is not null)
            Console.WriteLine($"[FlexQuery] [{e.QueryId}] Materialization failed in {e.Duration.TotalMilliseconds:F1}ms: {e.Exception.Message}");
        else
            Console.WriteLine($"[FlexQuery] [{e.QueryId}] Materialized in {e.Duration.TotalMilliseconds:F1}ms");
        return ValueTask.CompletedTask;
    }
}
