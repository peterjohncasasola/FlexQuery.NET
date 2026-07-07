using System.Diagnostics;
using FlexQuery.NET.Execution;

namespace FlexQuery.NET.Internal;

internal sealed class FlexQueryExecutionContext(
    FlexQueryExecutionConfig config,
    CancellationToken cancellationToken)
{
    public IFlexQueryExecutionListener? Listener { get; } = config.Listener;
    public Guid QueryId { get; } = Guid.NewGuid();
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
}
