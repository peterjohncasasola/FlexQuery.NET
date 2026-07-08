using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Execution;

/// <summary>
/// Collapses the repeated "if there's a listener, build the event, await it"
/// pattern used throughout the FlexQueryAsync EF Core execution pipeline into one
/// call per event type.
/// </summary>
internal static class FlexQueryExecutionNotifier
{
    public static async Task NotifyParsedAsync(this FlexQueryExecutionContext? ctx, QueryOptions options)
    {
        if (ctx?.Listener is null) return;
        await ctx.Listener.QueryParsedAsync(
            new QueryParsedEvent(ctx.QueryId, options, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
            ctx.CancellationToken);
    }

    /// <summary>
    /// Notifies the listener that translation finished. <paramref name="parameters"/> is only
    /// invoked when a listener is actually attached — <c>ToQueryString()</c> and SQL formatting
    /// are not free, so we don't pay for them when nobody's listening.
    /// </summary>
    public static async Task NotifyTranslatedAsync(
        this FlexQueryExecutionContext? ctx,
        string? sql,
        IReadOnlyList<QueryParameter>? parameters)
    {
        if (ctx?.Listener is null) return;

        await ctx.Listener.QueryTranslatedAsync(
            new QueryTranslatedEvent(ctx.QueryId, sql, parameters, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
            ctx.CancellationToken);
    }

    public static async Task NotifyExecutedAsync(this FlexQueryExecutionContext? ctx, int? rowCount, Exception? error = null)
    {
        if (ctx?.Listener is null) return;
        await ctx.Listener.QueryExecutedAsync(
            new QueryExecutedEvent(ctx.QueryId, rowCount, error, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
            ctx.CancellationToken);
    }

    public static async Task NotifyMaterializedAsync(this FlexQueryExecutionContext? ctx, QueryResult<object>? result, Exception? error = null)
    {
        if (ctx?.Listener is null) return;
        await ctx.Listener.QueryMaterializedAsync(
            new QueryMaterializedEvent(ctx.QueryId, result, error, ctx.Stopwatch.Elapsed, DateTimeOffset.UtcNow),
            ctx.CancellationToken);
    }
    
}