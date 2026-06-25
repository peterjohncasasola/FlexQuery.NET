# FlexQuery.NET

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)

Dynamic filtering, sorting, paging, and projection for `IQueryable` in .NET.

FlexQuery.NET is the core package. It provides the query parsing engine, expression builders, validation pipeline, and all core models.

## When to Use This Package

Install this package if you want to parse, validate, and compose dynamic queries independently of any specific data provider.
Use this package directly when building custom integrations or when implementing your own query execution pipeline.


## Installation

```bash
dotnet add package FlexQuery.NET
```


## Quick Start

```csharp
using FlexQuery.NET;

var parameters = new FlexQueryParameters
{
    Filter = "status:eq:active",
    Sort = "createdAt:desc",
    Page = 1,
    PageSize = 20
};

var options = parameters.ToQueryOptions();

var query = _context.Users.Apply(options);

// Execute using your preferred provider
```

## Features

- **Query Parsing** — Auto-detects DSL, JSON, and Indexed query formats via `QueryOptionsParser.Parse()`
- **Filtering** — 20+ operators (eq, contains, gt, between, in, etc.), nested AND/OR logic
- **Sorting** — Multi-field sorting with direction and aggregate sort support
- **Paging** — 1-based page indexing with configurable page size limits
- **Security** — Declare allowed/blocked fields per-endpoint via `BaseQueryOptions`
- **Validation** — Built-in field path, operator, and type validation with `ValidateOrThrow<T>()`
- **Diagnostics** — Optional `IFlexQueryExecutionListener` for observability
- **Query Parsing Cache** — Thread-safe parser cache for low-latency repeated queries

## Supported Query Formats

All formats are auto-detected — no configuration needed.

```http
DSL
GET /api/users?filter=age:gte:18&sort=name:asc

JSON
GET /api/users?filter={"logic":"and","filters":[{"field":"age","operator":"gte","value":18}]}

Indexed
GET /api/users?filter[0].field=age&filter[0].operator=gte&filter[0].value=18
```

## Related Packages

- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — Async execution for EF Core
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — SQL generation for Dapper
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core `[FieldAccess]` security
- [FlexQuery.NET.Diagnostics](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Diagnostics/README.md) — Execution diagnostics
- [FlexQuery.NET.Adapters.AgGrid](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.AgGrid/README.md) — AG Grid SSRM adapter
- [FlexQuery.NET.Adapters.Kendo](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.Kendo/README.md) — Kendo UI adapter
- [FlexQuery.NET.Parsers.Jql](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.Jql/README.md) — JQL parser
- [FlexQuery.NET.Parsers.MiniOData](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) — OData-compatible parser

## Full Documentation:

https://flexquery.vercel.app

- [Getting Started](https://flexquery.vercel.app/guide/getting-started)
- [Query Syntax Reference](https://flexquery.vercel.app/shared/query-language)
- [Security & Governance](https://flexquery.vercel.app/guide/security-governance)
- [API Reference](https://flexquery.vercel.app/shared/operators)
