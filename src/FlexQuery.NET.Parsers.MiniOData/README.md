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
builder.Services.AddFlexQueryMiniOData();
```

## Quick Start


```http
GET /api/products?$filter=Price gt 50 and Status eq 'Active'&$orderby=Name desc&$top=20&$skip=0
```


```csharp

[HttpGet("products")]
public async Task<IActionResult> GetProducts([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Products.FlexQueryAsync(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Status" };
    });

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
| `$skip` | Calculated from `Paging.Page` |
| `$expand` | `QueryOptions.Includes` |

## Features

- **Auto-Detection** — `MiniODataQueryParser` detects OData parameters and parses them automatically
- **No Migration Required** — Both OData and native FlexQuery syntax work on the same endpoints
- **Seamless Integration** — Register once to enable automatic OData parameter parsing
- **Standalone Parsing** — `ODataQueryParameterParser.Parse()` for direct OData-to-`QueryOptions` conversion

## Known Limitations

- The parser implements a subset of the full OData specification — not all OData functions and expressions are supported
- `$expand` support is limited to simple navigation property includes
- `$count` aggregation semantics differ from OData's `$inlinecount`

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.Jql](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.Jql/README.md) — Alternative parser for JQL syntax

## Documentation

- [MiniOData Integration Guide](https://flexquery.vercel.app/adapters/miniodata)
- [Query Formats Guide](https://flexquery.vercel.app/guide/query-formats)
