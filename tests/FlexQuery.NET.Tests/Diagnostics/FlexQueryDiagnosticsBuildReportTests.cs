using System.Diagnostics;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;
using Xunit;

namespace FlexQuery.NET.Tests.Diagnostics;

/// <summary>
/// Covers <see cref="FlexQueryDiagnosticsCollector.BuildReport"/> timeline/duration math,
/// partial-stage chains, ordering, and the QueryId fallback resolution across stages.
/// </summary>
public class FlexQueryDiagnosticsBuildReportTests
{
    private static readonly Guid QueryId = Guid.NewGuid();

    [Fact]
    public void BuildReport_FullChain_ComputesPerStageDurationsAndTotal()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        var baseTime = DateTimeOffset.UtcNow;

        // Cumulative durations: parsed=10, translated=30, executed=50, materialized=60 (ms).
        var parsed = new QueryParsedEvent(QueryId, new QueryOptions(), TimeSpan.FromMilliseconds(10), baseTime.AddMilliseconds(10));
        var translated = new QueryTranslatedEvent(QueryId, "SELECT 1", null, TimeSpan.FromMilliseconds(30), baseTime.AddMilliseconds(40));
        var executed = new QueryExecutedEvent(QueryId, 7, null, TimeSpan.FromMilliseconds(50), baseTime.AddMilliseconds(90));
        var materialized = new QueryMaterializedEvent(QueryId, MakeResult(2), null, TimeSpan.FromMilliseconds(60), baseTime.AddMilliseconds(150));

        collector.QueryParsedAsync(parsed, CancellationToken.None);
        collector.QueryTranslatedAsync(translated, CancellationToken.None);
        collector.QueryExecutedAsync(executed, CancellationToken.None);
        collector.QueryMaterializedAsync(materialized, CancellationToken.None);

        var report = collector.BuildReport(provider: "EFCore", translator: "EfCoreTranslator");

        report.QueryId.Should().Be(QueryId);
        report.Provider.Should().Be("EFCore");
        report.Translator.Should().Be("EfCoreTranslator");
        report.Sql.Should().Be("SELECT 1");
        report.Rows.Should().Be(2);

        // Total = last.End - first.Start = 150ms - 0ms
        report.Duration.TotalMs.Should().BeApproximately(150, 0.001);
        report.Duration.ParseMs.Should().BeApproximately(10, 0.001);
        report.Duration.TranslateMs.Should().BeApproximately(20, 0.001); // 30 - 10
        report.Duration.DatabaseMs.Should().BeApproximately(20, 0.001); // 50 - 30
        report.Duration.MaterializeMs.Should().BeApproximately(10, 0.001); // 60 - 50

        report.Timeline.Should().HaveCount(4);
        report.Timeline[0].Stage.Should().Be("Parsing");
        report.Timeline[1].Stage.Should().Be("Translation");
        report.Timeline[2].Stage.Should().Be("DatabaseExecution");
        report.Timeline[3].Stage.Should().Be("Materialization");
    }

    [Fact]
    public void BuildReport_OnlyParsed_HasSingleEntry()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        collector.QueryParsedAsync(
            new QueryParsedEvent(QueryId, new QueryOptions(), TimeSpan.FromMilliseconds(5), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var report = collector.BuildReport();

        report.QueryId.Should().Be(QueryId);
        report.Rows.Should().BeNull();
        report.Duration.ParseMs.Should().BeApproximately(5, 0.001);
        report.Duration.TranslateMs.Should().BeNull();
        report.Duration.DatabaseMs.Should().BeNull();
        report.Duration.MaterializeMs.Should().BeNull();
        report.Timeline.Should().ContainSingle().Which.Stage.Should().Be("Parsing");
    }

    [Fact]
    public void BuildReport_OnlyExecuted_UsesExecutedAsParseDuration()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, 3, null, TimeSpan.FromMilliseconds(12), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var report = collector.BuildReport();

        report.QueryId.Should().Be(QueryId);
        report.Rows.Should().Be(3);
        report.Duration.DatabaseMs.Should().BeApproximately(12, 0.001);
        report.Duration.ParseMs.Should().BeNull();
        report.Timeline.Should().ContainSingle().Which.Stage.Should().Be("DatabaseExecution");
    }

    [Fact]
    public void BuildReport_ParsedAndExecuted_MaterializeIsNull()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        collector.QueryParsedAsync(
            new QueryParsedEvent(QueryId, new QueryOptions(), TimeSpan.FromMilliseconds(8), DateTimeOffset.UtcNow),
            CancellationToken.None);
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, 4, null, TimeSpan.FromMilliseconds(20), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var report = collector.BuildReport();

        report.Duration.ParseMs.Should().BeApproximately(8, 0.001);
        report.Duration.DatabaseMs.Should().BeApproximately(12, 0.001); // 20 - 8
        report.Duration.MaterializeMs.Should().BeNull();
        report.Timeline.Should().HaveCount(2);
    }

    [Fact]
    public void BuildReport_WithExecutedException_SurfacesException()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        var ex = new InvalidOperationException("boom");
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, null, ex, TimeSpan.FromMilliseconds(5), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var report = collector.BuildReport();

        report.Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void BuildReport_WithMaterializedException_PrefersMaterializedException()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        var execEx = new Exception("exec");
        var matEx = new Exception("mat");
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, null, execEx, TimeSpan.FromMilliseconds(5), DateTimeOffset.UtcNow),
            CancellationToken.None);
        collector.QueryMaterializedAsync(
            new QueryMaterializedEvent(QueryId, null, matEx, TimeSpan.FromMilliseconds(8), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var report = collector.BuildReport();

        report.Exception.Should().BeSameAs(matEx);
    }

    [Fact]
    public void BuildReport_QueryIdFallsBackAcrossStages()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        // Only an executed event provides the id.
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, 1, null, TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow),
            CancellationToken.None);

        collector.BuildReport().QueryId.Should().Be(QueryId);
    }

    [Fact]
    public void BuildReport_NoEvents_ReturnsEmptyTimelineAndZeroDuration()
    {
        var collector = new FlexQueryDiagnosticsCollector();

        var report = collector.BuildReport();

        report.QueryId.Should().Be(Guid.Empty);
        report.Timeline.Should().BeEmpty();
        report.Duration.TotalMs.Should().Be(0);
    }

    [Fact]
    public void Clear_RemovesEventsSoBuildReportIsEmpty()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        collector.QueryParsedAsync(
            new QueryParsedEvent(QueryId, new QueryOptions(), TimeSpan.Zero, DateTimeOffset.UtcNow),
            CancellationToken.None);
        collector.QueryExecutedAsync(
            new QueryExecutedEvent(QueryId, 1, null, TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow),
            CancellationToken.None);

        collector.Clear();

        collector.ParsedEvents.Should().BeEmpty();
        collector.ExecutedEvents.Should().BeEmpty();
        collector.BuildReport().Timeline.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentRecording_AllEventsPreserved()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        const int count = 50;

        var tasks = Enumerable.Range(0, count).Select(i =>
        {
            var id = Guid.NewGuid();
            return Task.Run(async () =>
            {
                await collector.QueryParsedAsync(new QueryParsedEvent(id, new QueryOptions(), TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow), CancellationToken.None);
                await collector.QueryExecutedAsync(new QueryExecutedEvent(id, 1, null, TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow), CancellationToken.None);
            });
        }).ToArray();

        await Task.WhenAll(tasks);

        collector.ParsedEvents.Should().HaveCount(count);
        collector.ExecutedEvents.Should().HaveCount(count);
    }

    [Fact]
    public void Snapshots_AreCopies_NotLiveReferences()
    {
        var collector = new FlexQueryDiagnosticsCollector();
        collector.QueryParsedAsync(
            new QueryParsedEvent(QueryId, new QueryOptions(), TimeSpan.Zero, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var snapshot1 = collector.ParsedEvents;
        collector.QueryParsedAsync(
            new QueryParsedEvent(Guid.NewGuid(), new QueryOptions(), TimeSpan.Zero, DateTimeOffset.UtcNow),
            CancellationToken.None);

        snapshot1.Should().HaveCount(1);
        collector.ParsedEvents.Should().HaveCount(2);
    }

    private static object MakeResult(int rows) =>
        new QueryResult<object> { Data = Enumerable.Range(0, rows).Select(_ => new object()).ToList() };
}
