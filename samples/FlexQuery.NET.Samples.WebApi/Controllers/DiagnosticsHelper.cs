using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

internal static class DiagnosticsHelper
{
    public static object BuildReportShape(
        FlexQueryDiagnosticsReport report,
        string? overrideSql = null,
        string? overrideProvider = null,
        string? overrideTranslator = null) => new
    {
        queryId = report.QueryId == Guid.Empty ? null : (Guid?)report.QueryId,
        provider = overrideProvider ?? report.Provider,
        translator = overrideTranslator ?? report.Translator,
        rows = report.Rows,
        sql = overrideSql ?? report.Sql,
        duration = report.Duration == null ? null : new
        {
            totalMs = report.Duration.TotalMs > 0 ? Math.Round(report.Duration.TotalMs, 2) : (double?)null,
            parseMs = report.Duration.ParseMs is { } p ? Math.Round(p, 2) : (double?)null,
            translateMs = report.Duration.TranslateMs is { } t ? Math.Round(t, 2) : (double?)null,
            databaseMs = report.Duration.DatabaseMs is { } d ? Math.Round(d, 2) : (double?)null,
            materializeMs = report.Duration.MaterializeMs is { } m ? Math.Round(m, 2) : (double?)null
        },
        timeline = report.Timeline?.Select(t => new
        {
            stage = t.Stage,
            startUtc = t.StartUtc,
            endUtc = t.EndUtc,
            durationMs = Math.Round(t.DurationMs, 2)
        }).ToArray()
    };

    public static object BuildManualShape(
        string? sql = null,
        int? rows = null,
        double? totalDurationMs = null,
        string? provider = null,
        string? translator = null) => new
    {
        queryId = (Guid?)null,
        provider,
        translator,
        rows,
        sql,
        duration = totalDurationMs.HasValue ? new
        {
            totalMs = Math.Round(totalDurationMs.Value, 2),
            parseMs = (double?)null,
            translateMs = (double?)null,
            databaseMs = (double?)null,
            materializeMs = (double?)null
        } : null,
        timeline = (object[]?)null
    };

    public static int CountFilters(FilterGroup? g)
    {
        if (g is null) return 0;
        var top = g.Filters.Count;
        foreach (var child in g.Groups)
            top += CountFilters(child);
        return top;
    }
}
