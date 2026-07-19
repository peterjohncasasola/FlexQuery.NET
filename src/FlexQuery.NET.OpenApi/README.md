# FlexQuery.NET.OpenApi

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.OpenApi.svg)](https://www.nuget.org/packages/FlexQuery.NET.OpenApi)

OpenAPI/Swagger documentation and examples for FlexQuery.NET endpoints.

## When to Use This Package

Install this package when you expose FlexQuery-enabled endpoints and want automatic OpenAPI schema descriptions, parameter documentation, and canonical request/response examples in your Swagger UI or OpenAPI document.

## Installation

```bash
dotnet add package FlexQuery.NET.OpenApi
```

## Registration

```csharp
builder.Services.AddFlexQueryOpenApi();
```

This registers schema and operation transformers that enrich your OpenAPI document with:
- Descriptions for all FlexQuery model types (`FlexQueryRequest`, `FlexQueryParameters`, `QueryResult<T>`, etc.)
- Descriptions for query parameters (`filter`, `select`, `sort`, `page`, `pageSize`, `includeCount`)
- Canonical, production-quality examples for `FlexQueryRequest`, `FlexQueryParameters`, and `QueryResult<T>`

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddFlexQueryOpenApi();

builder.Services.AddOpenApi(options =>
{
    options.AddFlexQuery();
});

var app = builder.Build();

app.MapOpenApi();
app.Run();
```

## Features

- **Schema Descriptions** — Human-readable descriptions for all FlexQuery model types (`FlexQueryRequest`, `FlexQueryParameters`, `QueryResult<T>`, `FilterGroup`, `FilterCondition`, `SortNode`, `PagingOptions`, `Aggregate`, `HavingNode`, `IncludeNode`, `ProjectionMode`, `LogicOperator`, `AggregateFunction`)
- **Parameter Documentation** — Descriptions for query parameters (`filter`, `select`, `sort`, `page`, `pageSize`, `include`)
- **Governance-Aware Descriptions** — When `[FieldAccess]` attributes are applied to controllers or actions, parameter descriptions are automatically enriched with allowed/blocked fields, filterable fields, sortable fields, and expandable includes. A structured `x-flexquery` vendor extension is also emitted in the OpenAPI operation.
- **Canonical Examples** — Production-quality, strongly typed examples for `FlexQueryRequest`, `FlexQueryParameters`, and `QueryResult<T>` that immediately demonstrate FlexQuery capabilities
- **Zero Configuration** — Single registration call, no options, no builders
- **Minimal Public API** — Two extension methods, everything else internal

## FieldAccess Governance Integration

When you apply `[FieldAccess]` attributes to your controllers or actions, the OpenAPI transformer automatically enriches parameter descriptions and emits a structured `x-flexquery` vendor extension.

### Controller Example

```csharp
using FlexQuery.NET.AspNetCore.Attributes;

[FieldAccess(
    Selectable = new[] { "Id", "OrderNo", "Total" },
    Filterable = new[] { "Status", "Total" },
    Sortable = new[] { "CreatedAt" },
    AllowedIncludes = new[] { "Customer", "Items" })]
public class OrdersController : ControllerBase
{
    [HttpGet]
    public Task<List<Order>> Get(CancellationToken ct) => ...;
}
```

### Before

In Swagger UI, the `select` parameter shows only the base description:

```
select

Comma-separated field paths to include in the result (e.g., Id,FirstName,Email).
```

### After

With `[FieldAccess]`, the description is enriched with governance details:

```
select

Comma-separated field paths to include in the result (e.g., Id,FirstName,Email).

Allowed fields

• Id
• OrderNo
• Total
```

### Vendor Extension

The OpenAPI operation also includes a structured `x-flexquery` extension:

```yaml
x-flexquery:
  governance:
    allowedFields:
      - Id
      - OrderNo
      - Total
    filterableFields:
      - Status
      - Total
    sortableFields:
      - CreatedAt
    allowedIncludes:
      - Customer
      - Items
```

Only explicitly configured properties are emitted. Unconfigured properties (e.g., `blockedFields`, `groupableFields`, `maxDepth`) are omitted.

### Notes

- Requires `AddControllers()` or `AddMvc()` in your application setup.
- Action-level attributes take precedence over controller-level attributes.
- If no `[FieldAccess]` attribute is found, parameter descriptions remain unchanged and no `x-flexquery` extension is added.

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core integration
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — EF Core execution
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Dapper execution

## Documentation

- [OpenAPI Integration](https://flexquery.vercel.app/guide/openapi-integration)
- [Swagger Integration](https://flexquery.vercel.app/guide/swagger-integration)