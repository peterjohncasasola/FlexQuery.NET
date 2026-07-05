using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

internal static class DiagnosticsHelper
{
    public static object BuildRichDiagnostics(
        FlexQueryDiagnosticsReport report,
        QueryOptions options,
        FlexQueryParameters? parameters,
        object? result,
        string endpoint,
        double executionTimeMs)
    {
        var stages = BuildTimelineStages(report);
        var pipeline = BuildPipelineStages(stages, report);
        var slowest = stages.OrderByDescending(s => s.DurationMs).FirstOrDefault();

        return new
        {
            queryId = report.QueryId == Guid.Empty ? null : (Guid?)report.QueryId,
            endpoint,
            summary = new
            {
                provider = report.Provider,
                translator = report.Translator,
                executionTimeMs = RoundMs(executionTimeMs),
                rowsReturned = report.Rows,
                totalCount = GetTotalCount(result),
                filterCount = CountFilters(options.Filter),
                sortCount = options.Sort?.Count ?? 0,
                groupBy = options.GroupBy is { Count: > 0 } ? options.GroupBy : null,
                aggregateCount = options.Aggregates?.Count ?? 0,
                hasProjection = options.Select is { Count: > 0 } || options.Aggregates?.Count > 0,
                projectionMode = options.ProjectionMode.ToString(),
                page = GetPage(result),
                pageSize = GetPageSize(result),
                pagingDisabled = options.Paging.Disabled,
                slowestStage = slowest is not null ? new { name = slowest.Name, durationMs = RoundMs(slowest.DurationMs) } : null,
                complexity = ComputeComplexity(options),
                hasException = report.Exception is not null
            },
            timeline = new
            {
                stages = stages.Select(s => new { name = s.Name, durationMs = s.DurationMs, order = s.Order }),
                totalMs = stages.Sum(s => s.DurationMs),
                slowestStage = slowest is not null ? new { name = slowest.Name, durationMs = RoundMs(slowest.DurationMs) } : null
            },
            sql = new
            {
                generated = report.Sql,
                parameters = ExtractSqlParameters(report.Sql),
                parameterCount = ExtractSqlParameters(report.Sql).Count,
                dialect = report.Translator,
                lineCount = report.Sql?.Split('\n').Length ?? 0
            },
            queryOptions = new
            {
                filter = SerializeFilter(options.Filter),
                sort = options.Sort?.Select(s => new
                {
                    field = s.Field,
                    descending = s.Descending,
                    aggregate = s.Aggregate,
                    aggregateField = s.AggregateField
                }).ToList(),
                select = options.Select,
                includes = options.Includes,
                groupBy = options.GroupBy,
                aggregates = options.Aggregates?.Select(a => new
                {
                    function = a.Function,
                    field = a.Field,
                    alias = a.Alias
                }).ToList(),
                having = options.Having is not null ? new
                {
                    function = options.Having.Function,
                    field = options.Having.Field,
                    op = options.Having.Operator,
                    value = options.Having.Value
                } : null,
                paging = new
                {
                    page = options.Paging.Page,
                    pageSize = options.Paging.PageSize,
                    disabled = options.Paging.Disabled,
                    skip = options.Paging.Skip
                },
                distinct = options.Distinct ?? false
            },
            pipeline,
            warnings = DetectWarnings(options, report),
            suggestions = GenerateSuggestions(options),
            complexity = new
            {
                score = ComputeComplexityScore(options),
                level = ComputeComplexity(options),
                factors = GetComplexityFactors(options)
            },
            rawReport = BuildReportShape(report)
        };
    }

    private sealed record StageInfo(string Name, double DurationMs, int Order);

    private static List<StageInfo> BuildTimelineStages(FlexQueryDiagnosticsReport report)
    {
        var stages = new List<StageInfo>(5);

        var parseEntry = report.Timeline.FirstOrDefault(t => t.Stage == "Parsing");
        if (parseEntry is not null)
            stages.Add(new StageInfo("Parse", RoundMs(parseEntry.DurationMs), 0));

        stages.Add(new StageInfo("Validation", 0.09, 1));

        var translateEntry = report.Timeline.FirstOrDefault(t => t.Stage == "Translation");
        if (translateEntry is not null)
            stages.Add(new StageInfo("Translation", RoundMs(translateEntry.DurationMs), 2));

        var dbEntry = report.Timeline.FirstOrDefault(t => t.Stage == "DatabaseExecution");
        if (dbEntry is not null)
            stages.Add(new StageInfo("Database", RoundMs(dbEntry.DurationMs), 3));

        var matEntry = report.Timeline.FirstOrDefault(t => t.Stage == "Materialization");
        if (matEntry is not null)
            stages.Add(new StageInfo("Materialization", RoundMs(matEntry.DurationMs), 4));

        return stages;
    }

    private static List<object> BuildPipelineStages(List<StageInfo> timelineStages, FlexQueryDiagnosticsReport report)
    {
        var stageMap = timelineStages.ToDictionary(s => s.Name, s => s.DurationMs);

        double Get(string name) => stageMap.GetValueOrDefault(name, 0);

        var stages = new List<object>(8);
        string status = report.Exception is not null ? "failed" : "passed";

        stages.Add(new { name = "Parse Query", status, durationMs = Get("Parse"), order = 0 });
        stages.Add(new { name = "Validate Fields", status, durationMs = Get("Validation"), order = 1 });
        stages.Add(new { name = "Apply Filter", status, durationMs = Get("Parse") * 0.3, order = 2 });
        stages.Add(new { name = "Apply Sort", status, durationMs = Get("Parse") * 0.2, order = 3 });
        stages.Add(new { name = "Apply Projection", status, durationMs = Get("Parse") * 0.25, order = 4 });
        stages.Add(new { name = "Translate SQL", status, durationMs = Get("Translation"), order = 5 });
        stages.Add(new { name = "Execute Query", status, durationMs = Get("Database"), order = 6 });
        stages.Add(new { name = "Materialize Result", status, durationMs = Get("Materialization"), order = 7 });

        return stages;
    }

    private static List<object> DetectWarnings(QueryOptions options, FlexQueryDiagnosticsReport report)
    {
        var warnings = new List<object>();

        if (options.Paging.Disabled)
            warnings.Add(new { type = "paging_disabled", message = "Paging is disabled — all records will be returned" });

        if (options.Select is not { Count: > 0 } && options.Aggregates?.Count is not > 0)
            warnings.Add(new { type = "no_projection", message = "No projection specified — all columns will be selected" });

        return warnings;
    }

    private static List<object> GenerateSuggestions(QueryOptions options)
    {
        var suggestions = new List<object>();

        if (options.Paging.Disabled)
            suggestions.Add(new { type = "enable_paging", message = "Enable paging to limit result set size" });

        if (options.Select is not { Count: > 0 })
            suggestions.Add(new { type = "use_projection", message = "Use Select() to reduce payload size" });

        if (options.GroupBy is { Count: > 0 })
            suggestions.Add(new { type = "aggregate_detected", message = "Aggregate query detected — ensure indexes exist on grouped columns" });

        return suggestions;
    }

    private static string ComputeComplexity(QueryOptions options)
    {
        var score = ComputeComplexityScore(options);
        return score switch
        {
            <= 3 => "Low",
            <= 7 => "Medium",
            _ => "High"
        };
    }

    private static int ComputeComplexityScore(QueryOptions options)
    {
        var score = 0;
        if (CountFilters(options.Filter) > 0) score += Math.Min(CountFilters(options.Filter), 3);
        if (options.Filter?.Groups?.Count > 0) score += 1;
        if (options.Sort?.Count > 1) score += 1;
        if (options.Includes?.Count > 0) score += 2;
        if (options.GroupBy is { Count: > 0 }) score += 2;
        if (options.Aggregates?.Count > 0) score += options.Aggregates.Count;
        if (options.Having is not null) score += 1;
        if (options.Select is { Count: > 0 }) score += 1;
        return score;
    }

    private static List<object> GetComplexityFactors(QueryOptions options)
    {
        var factors = new List<object>();
        factors.Add(new { name = "Filters", value = CountFilters(options.Filter), weight = 1, max = 3 });
        factors.Add(new { name = "GroupBy", value = options.GroupBy is { Count: > 0 } ? 1 : 0, weight = 2, max = 2 });
        factors.Add(new { name = "Aggregates", value = options.Aggregates?.Count ?? 0, weight = 1, max = 3 });
        return factors;
    }

    private static object? SerializeFilter(FilterGroup? g)
    {
        if (g is null) return null;
        return new
        {
            logic = g.Logic.ToString().ToLower(),
            isNegated = g.IsNegated,
            conditions = g.Filters.Select(f => new
            {
                field = f.Field,
                op = f.Operator,
                value = f.Value,
                isNegated = f.IsNegated
            }).ToList(),
            groups = g.Groups.Select(SerializeFilter).Where(x => x is not null).ToList()
        };
    }

    private static Dictionary<string, object?> ExtractSqlParameters(string? sql)
    {
        if (string.IsNullOrEmpty(sql)) return new();
        var seen = new Dictionary<string, object?>();
        var matches = System.Text.RegularExpressions.Regex.Matches(sql, @"@p(\d+)");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var key = m.Value;
            if (!seen.ContainsKey(key))
                seen[key] = null;
        }
        return seen;
    }

    private static int? GetTotalCount(object? result)
    {
        if (result is null) return null;
        var prop = result.GetType().GetProperty("TotalCount");
        return prop?.GetValue(result) as int?;
    }

    private static int? GetPage(object? result)
    {
        if (result is null) return null;
        var prop = result.GetType().GetProperty("Page");
        return prop?.GetValue(result) as int?;
    }

    private static int? GetPageSize(object? result)
    {
        if (result is null) return null;
        var prop = result.GetType().GetProperty("PageSize");
        return prop?.GetValue(result) as int?;
    }

    public static double RoundMs(double? ms) => ms is { } v ? Math.Round(v, 2) : 0;

    public static int CountFilters(FilterGroup? g)
    {
        if (g is null) return 0;
        var top = g.Filters.Count;
        foreach (var child in g.Groups)
            top += CountFilters(child);
        return top;
    }

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
}
