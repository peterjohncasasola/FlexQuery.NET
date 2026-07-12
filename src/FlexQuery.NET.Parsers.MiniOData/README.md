# FlexQuery.NET.Parsers.MiniOData

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.MiniOData.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.MiniOData)

Lightweight OData-compatible query syntax parser for FlexQuery.NET.

## When to Use This Package

Install this package if you are migrating from OData or need to support existing OData clients.
The parser allows OData and native FlexQuery query syntax to coexist on the same endpoint, enabling gradual migration without changing controller code.

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.MiniOData
```

## Registration

```csharp
using FlexQuery.NET.Parsers.MiniOData;

MiniOData.Register();
```

## Quick Start


```http
GET /api/products?$filter=Price gt 50 and Status eq 'Active'&$orderby=Name desc&$top=20&$skip=0
```


```csharp

[HttpGet("products")]
public async Task<IActionResult> GetProducts([FromQuery] MiniODataRequest request)
{
    var options = request.ToQueryOptions();

    var result = await _context.Products.FlexQueryAsync(options, cfg =>
    {
        cfg.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Status" };
    });

    return Ok(result);
}
```

## Supported OData Features

| Feature    | Status      | Maps To / Notes                  |
| ---------- | ----------- | -------------------------------- |
| `$filter`  | ✅ Supported | `QueryOptions.Filter` — strict grammar validation |
| `$orderby` | ✅ Supported | `QueryOptions.Sort` — supports `asc` / `desc` |
| `$select`  | ✅ Supported | `QueryOptions.Select` — supports nested property paths |
| `$expand`  | ✅ Supported | `QueryOptions.Includes` — flat navigation paths only |
| `$top`     | ✅ Supported | `Paging.PageSize` |
| `$skip`     | ✅ Supported | Calculated from `Paging.Page` |
| `$count`   | ✅ Supported | Maps to `IncludeCount` |

## Features

- **OData-Compatible Syntax** — Supports `$filter`, `$orderby`, `$select`, `$expand`, `$top`, `$skip`, `$count`
- **No Migration Required** — Both OData and native FlexQuery syntax work on the same endpoints
- **Seamless Integration** — Register once via `MiniOData.Register()` to enable OData parameter parsing
- **Standalone Parsing** — `MiniODataRequest.ToQueryOptions()` for direct OData-to-`QueryOptions` conversion

## Deferred / Unsupported Features

MiniOData is a lightweight OData compatibility layer, not a complete OData implementation. The
following features are intentionally unavailable. Supplying an unsupported or deferred feature
fails with a clear `MiniODataParseException` rather than being silently ignored.

| Feature                               | Status          | Notes                                  |
| ------------------------------------- | --------------- | -------------------------------------- |
| `$apply`                              | ⏳ Deferred      | May be implemented in a future release |
| `$compute`                            | ❌ Not Supported | No current FlexQuery equivalent        |
| `$search`                             | ❌ Not Supported | No current FlexQuery equivalent        |
| `$levels`                             | ❌ Not Supported | No current FlexQuery equivalent        |
| `$ref`                                | ❌ Not Supported | No current FlexQuery equivalent        |
| Nested query options inside `$expand` | ❌ Not Supported | Use top-level query parameters instead |

## Known Limitations

- The parser implements a subset of the full OData specification — not all OData functions and expressions are supported
- `$expand` support is limited to flat navigation property includes (nested query options are unsupported)
- `$count` aggregation semantics differ from OData's `$inlinecount`

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.FQL](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.FQL/README.md) — Alternative parser for FQL syntax

## Documentation

- [MiniOData Integration Guide](https://flexquery.vercel.app/adapters/miniodata)
- [Query Formats Guide](https://flexquery.vercel.app/guide/query-formats)
