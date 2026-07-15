using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;
using Xunit;

namespace FlexQuery.NET.Tests.Diagnostics;

public class FlexQueryDiagnosticsCollectorTests
{
    [Fact]
    public async Task QueryParsedAsync_AndBuildReport_RecordsEvent()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        var timestamp = DateTimeOffset.UtcNow;

        await collector.QueryParsedAsync(new QueryParsedEvent(
            Guid.NewGuid(),
            new QueryOptions(),
            TimeSpan.FromMilliseconds(10),
            timestamp), CancellationToken.None);

        collector.ParsedEvents.Should().HaveCount(1);
        collector.ParsedEvents[0].Duration.TotalMilliseconds.Should().Be(10);
    }

    [Fact]
    public async Task BuildReport_WithNoEvents_ReturnsEmptyTimeline()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        var report = collector.BuildReport();

        report.Timeline.Should().BeEmpty();
        report.Duration.TotalMs.Should().Be(0);
    }

    [Fact]
    public async Task Clear_RemovesAllRecordedEvents()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        await collector.QueryParsedAsync(new QueryParsedEvent(Guid.NewGuid(), new QueryOptions(), TimeSpan.Zero, DateTimeOffset.UtcNow), CancellationToken.None);

        collector.Clear();

        collector.ParsedEvents.Should().BeEmpty();
    }
}
