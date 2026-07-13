# FlexQuery.NET

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)

A flexible query engine for .NET that enables dynamic filtering, sorting, projection, grouping, aggregation, paging, and validation over `IQueryable`.

FlexQuery.NET includes the native FlexQuery DSL out of the box and serves as the foundation of the FlexQuery ecosystem.

---

# Installation

```bash
dotnet add package FlexQuery.NET
```

---

# When to Use This Package

Install the core package when you want to:

- Build dynamic query APIs
- Parse and validate client query parameters
- Generate LINQ expression trees
- Support filtering, sorting, projection, grouping, and paging
- Build custom query execution pipelines
- Integrate FlexQuery with your own data provider

If you're using Entity Framework Core or Dapper, install the corresponding integration package.

---

# Example HTTP Request

The native FlexQuery DSL is included by default.

```http
GET /api/products?
filter=category:eq:electronics OR price:between:1000,5000
&select=id,name,price,supplier.name
&sort=price:desc,name:asc
&include=supplier,reviews
&page=1
&pageSize=20
&includeCount=true

```

---

# Features

- Dynamic filtering
- Projection (`select`)
- Multi-column sorting
- Navigation property includes
- Nested property paths
- Collection operators (`any`, `all`, `count`)
- Grouping
- Aggregate functions
- HAVING support
- Offset pagination
- Keyset (cursor) pagination
- Query validation
- Expression tree generation
- Provider-agnostic architecture
- Extensible parser pipeline

---

# Supported Query Parameters

| Parameter | Description |
|-----------|-------------|
| `filter` | Filter records |
| `select` | Select specific fields |
| `sort` | Sort one or more fields |
| `include` | Include related entities |
| `groupBy` | Group records |
| `aggregate` | Aggregate functions |
| `having` | HAVING clause |
| `page` | Page number |
| `pageSize` | Number of records per page |
| `cursor` | Cursor for keyset pagination |
| `useKeysetPagination` | Enables keyset pagination |
| `distinct` | Return distinct records |
| `includeCount` | Include the total record count |

---

# Query Languages

FlexQuery.NET includes the native FlexQuery DSL by default.

Additional query languages are available as optional packages.

| Query Language | Package |
|----------------|---------|
| Native FlexQuery DSL | Included |
| FQL (SQL-inspired) | FlexQuery.NET.Parsers.Fql |
| Mini OData | FlexQuery.NET.Parsers.MiniOData |

All query languages produce the same internal `QueryOptions` model, allowing them to share the same validation, expression building, and execution pipeline.

---

# Architecture

```
HTTP Request
      │
      ▼
Query Parser
      │
      ▼
QueryOptions
      │
      ▼
Validation
      │
      ▼
Expression Builder
      │
      ▼
Execution Provider
      ├── Entity Framework Core
      ├── Dapper
      └── Custom Provider
```

---

# Related Packages

| Package | Description |
|---------|-------------|
| FlexQuery.NET.EntityFrameworkCore | Entity Framework Core integration |
| FlexQuery.NET.Dapper | SQL generation for Dapper |
| FlexQuery.NET.AspNetCore | ASP.NET Core integration |
| FlexQuery.NET.Diagnostics | Diagnostics and execution events |
| FlexQuery.NET.Adapters.AgGrid | AG Grid Server-Side Row Model |
| FlexQuery.NET.Adapters.Kendo | Telerik Kendo UI integration |
| FlexQuery.NET.Parsers.Fql | SQL-inspired query language |
| FlexQuery.NET.Parsers.MiniOData | OData-compatible query language |
| FlexQuery.NET.OpenApi | OpenAPI/Swagger documentation and examples |

---

# Documentation

https://flexquery.vercel.app

- Getting Started
- Query Language (DSL)
- FQL
- Mini OData
- Operators
- Pagination
- Validation
- Entity Framework Core
- Dapper
- ASP.NET Core
- Adapters