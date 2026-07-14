using FlexQuery.NET.Diagnostics;
using Xunit;

namespace FlexQuery.NET.Tests.Diagnostics;

/// <summary>
/// Verifies the diagnostics report model types round-trip their init-only properties.
/// </summary>
public class FlexQueryDiagnosticsModelsTests
{
    [Fact]
    public void FlexQueryDiagnosticsReport_PropertiesRoundTrip()
    {
        var entry = new TimelineEntry
        {
            Stage = "Parsing",
            StartUtc = DateTimeOffset.UnixEpoch,
            EndUtc = DateTimeOffset.UnixEpoch.AddMilliseconds(5),
            DurationMs = 5
        };

        var duration = new DiagnosticsDuration
        {
            TotalMs = 5,
            ParseMs = 5,
            TranslateMs = null,
            DatabaseMs = null,
            MaterializeMs = null
        };

        var report = new FlexQueryDiagnosticsReport
        {
            QueryId = Guid.NewGuid(),
            Provider = "EFCore",
            Translator = "EfCoreTranslator",
            Sql = "SELECT 1",
            Rows = 3,
            Exception = new InvalidOperationException("x"),
            Duration = duration,
            Timeline = [entry]
        };

        report.QueryId.Should().NotBe(Guid.Empty);
        report.Provider.Should().Be("EFCore");
        report.Translator.Should().Be("EfCoreTranslator");
        report.Sql.Should().Be("SELECT 1");
        report.Rows.Should().Be(3);
        report.Exception.Should().NotBeNull();
        report.Duration.TotalMs.Should().Be(5);
        report.Timeline.Should().ContainSingle().Which.Stage.Should().Be("Parsing");
    }

    [Fact]
    public void TimelineEntry_DefaultsAreSafe()
    {
        var entry = new TimelineEntry();

        entry.Stage.Should().Be(string.Empty);
        entry.DurationMs.Should().Be(0);
    }

    [Fact]
    public void DiagnosticsDuration_DefaultsAreSafe()
    {
        var duration = new DiagnosticsDuration();

        duration.TotalMs.Should().Be(0);
        duration.ParseMs.Should().BeNull();
        duration.TranslateMs.Should().BeNull();
        duration.DatabaseMs.Should().BeNull();
        duration.MaterializeMs.Should().BeNull();
    }
}
