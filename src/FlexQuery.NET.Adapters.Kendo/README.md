# FlexQuery.NET.Adapters.Kendo

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Adapters.Kendo.svg)](https://www.nuget.org/packages/FlexQuery.NET.Adapters.Kendo)

Kendo UI DataSource adapter for FlexQuery.NET.

## When to Use This Package

Install this package if your application uses Kendo UI DataSource for server-side filtering, sorting, and paging. It translates Kendo's DataSource request parameters — `filter`, `sort`, `take`, `skip` — into FlexQuery's canonical `QueryOptions`.

## Installation

```bash
dotnet add package FlexQuery.NET.Adapters.Kendo
```

## Quick Start

```csharp
using FlexQuery.NET.Adapters.Kendo;

[HttpPost("grid-data")]
public async Task<IActionResult> GetGridData([FromBody] KendoRequest request)
{
    var options = request.ToQueryOptions();

    var result = await _context.Products.FlexQueryAsync(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
    });

    return Ok(result);
}
```

## Features

- **Request Parsing** — `KendoQueryOptionsParser.Parse()` converts Kendo DataSource JSON to `QueryOptions`
- **Filter Support** — Supports nested filter groups with `AND/OR` logic.
- **Sort Support** — `sort` array with `field` and `dir` (asc/desc)
- **Paging** — `take`/`skip` converted to FlexQuery's `Page`/`PageSize`
- **No Database Calls** — The adapter only transforms JSON — zero overhead

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — EF Core execution
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Dapper execution
- [FlexQuery.NET.Adapters.AgGrid](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.AgGrid/README.md) — Alternative adapter for AG Grid

## Documentation
Full guide:
- [Kendo Integration Guide](https://flexquery.vercel.app/adapters/kendo)
