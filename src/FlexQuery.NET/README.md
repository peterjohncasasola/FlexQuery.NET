# FlexQuery.NET

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)

Dynamic filtering, sorting, paging, and projection for `IQueryable` in .NET.

FlexQuery.NET is the core package. It provides the query parsing engine, expression builders, validation pipeline, and all core models.

## When to Use This Package

Install this package if you want to parse, validate, and compose dynamic queries independently of any specific data provider.
Use this package directly when building custom integrations or when implementing your own query execution pipeline. Most applications pair it with a provider package — Entity Framework Core or Dapper — that executes the parsed query.

## Installation

```bash
dotnet add package FlexQuery.NET
```

## Quick Start

```csharp
using FlexQuery.NET;
using FlexQuery.NET.Models;

var parameters = new FlexQueryParameters
{
    Filter = "status:eq:active",
    Sort = "createdAt:desc",
    Page = 1,
    PageSize = 20
};

// Parse into QueryOptions (uses the globally configured syntax)
var options = parameters.ToQueryOptions();

// Apply filter, sort, and paging to any IQueryable
var query = _context.Users.Apply(options);

// Execute using your preferred provider
```

## Features

- **Query Parsing** — Native DSL syntax with the parser registry ready for FQL and MiniOData parser packages
- **Filtering** — 20+ operators (eq, neq, gt, gte, lt, lte, contains, startswith, endswith, like, in, notin, between, isnull, isnotnull, any, all, count) with nested AND/OR logic
- **Sorting** — Multi-field sorting with direction and aggregate sort support
- **Paging** — 1-based page indexing with configurable page size limits, plus keyset (cursor) pagination support
- **Security** — Declare allowed/blocked fields, per-operation whitelists, and role-based access via `QueryGovernanceOptions`
- **Validation** — Built-in field path, operator, and type validation with structured errors (`ValidateOrThrow`)
- **Diagnostics** — Optional `IFlexQueryExecutionListener` for observability
- **Query Parsing Cache** — Thread-safe parser cache for low-latency repeated queries

## Supported Query Formats

The default syntax is the native DSL:

```http
GET /api/users?filter=age:gte:18&sort=name:asc
```

Alternative syntaxes are available as separate parser packages:

- **FQL** (SQL-inspired: `filter=age >= 18 AND status = 'Active'`) — `FlexQuery.NET.Parsers.Fql`
- **MiniOData** (OData-compatible: `$filter=age ge 18`) — `FlexQuery.NET.Parsers.MiniOData`

## Related Packages

- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — Async execution for EF Core
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — SQL generation for Dapper
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core `[FieldAccess]` security
- [FlexQuery.NET.OpenApi](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.OpenApi/README.md) — OpenAPI documentation
- [FlexQuery.NET.Diagnostics](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Diagnostics/README.md) — Execution diagnostics
- [FlexQuery.NET.Adapters.AgGrid](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.AgGrid/README.md) — AG Grid SSRM adapter
- [FlexQuery.NET.Adapters.Kendo](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.Kendo/README.md) — Kendo UI adapter
- [FlexQuery.NET.Parsers.Fql](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.Fql/README.md) — FQL parser
- [FlexQuery.NET.Parsers.MiniOData](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) — OData-compatible parser

## Full Documentation

https://flexquery.vercel.app

- [Getting Started](https://flexquery.vercel.app/docs/getting-started/first-query)
- [Query Syntax Reference](https://flexquery.vercel.app/docs/concepts/query-syntax)
- [Security & Governance](https://flexquery.vercel.app/docs/security)
- [API Reference](https://flexquery.vercel.app/docs/api-reference)
