# FlexQuery.NET.Parsers.MiniOData

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.MiniOData.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.MiniOData)

Lightweight OData-compatible query syntax parser for FlexQuery.NET.

## When to Use This Package

Install this package if you are migrating from OData or need to support existing OData clients. The parser allows OData-style parameters on the same endpoints that serve the native DSL, enabling gradual migration without changing controller code.

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.MiniOData
```

## Registration

Call `MiniOData.Register()` once at startup, then select the MiniOData syntax globally or per request:

```csharp
using FlexQuery.NET;
using FlexQuery.NET.Parsers.MiniOData;

MiniOData.Register();   // registers the MiniOData parser in the global parser registry

// Option A: set MiniOData as the application-wide default syntax
FlexQueryCore.Configure(options =>
{
    options.DefaultQuerySyntax = QuerySyntax.MiniOData;
});

// Option B: choose MiniOData per request
options.QuerySyntax = QuerySyntax.MiniOData;
```

## Quick Start

```http
GET /api/products?$filter=Price gt 50 and Status eq 'Active'&$orderby=Name desc&$top=20&$skip=0
```

```csharp
[HttpGet("products")]
public async Task<IActionResult> GetProducts(
    [FromQuery] FlexQueryParameters parameters,
    CancellationToken cancellationToken)
{
    var result = await _context.Products
        .FlexQueryAsync(parameters, opts =>
        {
            opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Status" };
        }, cancellationToken: cancellationToken);

    return Ok(result);
}
```

## Supported OData Parameters

| Parameter | Maps To |
|---|---|
| `$filter` | `QueryOptions.Filter` |
| `$orderby` | `QueryOptions.Sort` |
| `$select` | `QueryOptions.Select` |
| `$top` | `Paging.PageSize` |
| `$skip` | Calculated into `Paging.Page` |
| `$expand` | `QueryOptions.Includes` |
| `$count` | `QueryOptions.IncludeCount` |

`$apply` is explicitly rejected with a parse error in this version.

## Features

- **OData-Compatible Syntax** — `$filter`, `$orderby`, `$select`, `$top`, `$skip`, `$expand`, and `$count` on the same endpoints as the native DSL
- **Standalone Parsing** — Build a `MiniODataRequest` and call `ToQueryOptions()` for direct OData-to-`QueryOptions` conversion
- **Integration** — Registers into the global parser registry so OData queries flow through the same validation and execution pipeline

## Known Limitations

- The parser implements a subset of the OData specification — it is not a full OData implementation (no EDM metadata, batch requests, or delta tracking)
- `$expand` support is limited to navigation property includes
- `$apply` is not supported

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.Fql](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.Fql/README.md) — Alternative parser for FQL (SQL-inspired) syntax

## Documentation

- [Query Syntax](https://flexquery.vercel.app/docs/concepts/query-syntax)
