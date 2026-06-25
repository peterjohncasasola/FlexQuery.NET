# FlexQuery.NET.Diagnostics

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Diagnostics.svg)](https://www.nuget.org/packages/FlexQuery.NET.Diagnostics)

Execution diagnostics, observability, and timeline reporting for FlexQuery.NET queries.


## When to Use This Package

Install this package when you need to inspect, measure, or monitor FlexQuery.NET execution.

It provides execution listeners, timing reports, and pipeline diagnostics for debugging, performance analysis, and observability.


## Installation

```bash
dotnet add package FlexQuery.NET.Diagnostics
```


## Quick Start

```csharp
using FlexQuery.NET.Diagnostics;

var collector = new FlexQueryDiagnosticsCollector();

var result = await _context.Users.FlexQueryAsync(parameters, options =>
{
    options.AllowedFields = new HashSet<string> { "Id", "Name" };
}, execution =>
{
    execution.Listener = collector;
});

var report = collector.BuildReport();
Console.WriteLine($"Total: {report.Duration.TotalMs}ms");
Console.WriteLine($"  Parse:    {report.Duration.ParseMs}ms");
Console.WriteLine($"  Translate: {report.Duration.TranslateMs}ms");
Console.WriteLine($"  Database:  {report.Duration.DatabaseMs}ms");
Console.WriteLine($"  Materialize: {report.Duration.MaterializeMs}ms");

foreach (var entry in report.Timeline)
    Console.WriteLine($"{entry.Stage}: {entry.DurationMs}ms");
```

## Features

- **`FlexQueryDiagnosticsCollector`** — Thread-safe collector capturing all pipeline stage events
- **`ConsoleExecutionListener`** — Writes pipeline stage timing to `Console` for quick debugging
- **Diagnostics Report** — `BuildReport()` returns `FlexQueryDiagnosticsReport` with per-stage duration breakdown and timeline
- **4 Lifecycle Events** — `QueryParsed`, `QueryTranslated`, `QueryExecuted`, `QueryMaterialized`
- **Custom Listeners** — Implement `IFlexQueryExecutionListener` for custom logging, metrics, or OpenTelemetry integration

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine

## Documentation

- [Debugging & Diagnostics Guide](https://flexquery.vercel.app/guide/debugging)
