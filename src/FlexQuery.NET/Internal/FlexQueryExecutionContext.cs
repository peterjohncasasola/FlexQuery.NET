using System.Diagnostics;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Internal;

internal sealed class FlexQueryExecutionContext
{
    public FlexQueryExecutionContext(
        FlexQueryExecutionConfig config,
        CancellationToken cancellationToken)
    {
        Listener = config.Listener;
        CancellationToken = cancellationToken;
        QueryId = Guid.NewGuid();
    }

    public IFlexQueryExecutionListener? Listener { get; }
    public Guid QueryId { get; }
    public CancellationToken CancellationToken { get; }
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
}
