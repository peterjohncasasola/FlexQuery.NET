using System.Diagnostics;
using FlexQuery.NET.Execution;

namespace FlexQuery.NET.Internal;

internal sealed class FlexQueryExecutionContext(
    IFlexQueryExecutionListener listener,
    CancellationToken cancellationToken)
{
    public IFlexQueryExecutionListener? Listener { get; } = listener;
    public Guid QueryId { get; } = Guid.NewGuid();
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
}
