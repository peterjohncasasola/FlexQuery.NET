# FlexQuery.NET.Adapters.AgGrid

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Adapters.AgGrid.svg)](https://www.nuget.org/packages/FlexQuery.NET.Adapters.AgGrid)

AG Grid Server-Side Row Model (SSRM) adapter for FlexQuery.NET.

## When to Use This Package

Install this package if your application uses AG Grid Server-Side Row Model (SSRM).

The adapter translates AG Grid SSRM request payloads into FlexQuery `QueryOptions` and converts query results back into the response format expected by AG Grid.

## Installation

```bash
dotnet add package FlexQuery.NET.Adapters.AgGrid
```

## Quick Start

```csharp
using FlexQuery.NET.Adapters.AgGrid;

[HttpPost("grid-data")]
public async Task<IActionResult> GetGridData([FromBody] AgGridRequest request)
{
    var options = request.ToQueryOptions();

    var result = await _context.Products.FlexQueryAsync(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
    });

    var response = result.ToAgGridServerSideResponse(request);

    return Ok(response);
}
```

## Supported Features
- **Filtering**
- **Sorting**
- **Pagination**
- **Row Grouping**
- **Aggregations**
- **Projection**


## Features

- **Request Parsing** — `AgGridQueryOptionsParser.Parse()` converts AG Grid JSON to `QueryOptions`
- **Response Conversion** — `ToAgGridServerSideResponse()` returns data in AG Grid's expected format with group metadata (key, level, childCount)
- **Server-Side Row Model Support** — Pagination, filtering, sorting, row grouping, and aggregations
- **Aggregate Alias Mapping** — Single-aggregate columns map back to the original field name
- **camelCase Option** — PascalCase-to-camelCase conversion for row data dictionaries
- **No Database Calls** — The adapter only transforms JSON — zero overhead

## Filter Mapping

| AG Grid Filter | FlexQuery Operator |
|---|---|
| `equals` | `eq` |
| `notEqual` | `neq` |
| `contains` | `contains` |
| `lessThan` | `lt` |
| `greaterThan` | `gte` |
| `inRange` | `between` |

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — EF Core execution
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Dapper execution
- [FlexQuery.NET.Adapters.Kendo](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.Kendo/README.md) — Alternative adapter for Kendo UI

## Documentation

- [AG Grid Integration Guide](https://flexquery.vercel.app/adapters/ag-grid)
