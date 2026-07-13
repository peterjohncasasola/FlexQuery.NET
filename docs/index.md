---
layout: home

hero:
  name: FlexQuery.NET
  text: Turn your API into a secure, composable query engine
  tagline: Flexible querying for .NET IQueryable — filtering, sorting, paging, projection, validation, and field-level security.
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/peterjohncasasola/FlexQuery.NET
    - theme: alt
      text: API Reference
      link: /shared/operators

features:
  - icon: 🔍
    title: Dynamic Filtering
    details: Support for DSL and FQL query-string formats. Nested AND/OR, collection predicates (any/all), and 20+ operators out of the box.
  - icon: ↕️
    title: Sorting & Paging
    details: Multi-field sorting with aggregate sort support. Server-safe paging with automatic default ordering to prevent EF Core errors.
  - icon: 📐
    title: Projection & Aliasing
    details: Shape your API response at runtime. Select only the fields clients need — in Nested, Flat, or FlatMixed modes.
  - icon: 🔗
    title: Filtered Includes
    details: Include related collections with inline WHERE filters. A fully independent include pipeline that won't affect your root result count.
  - icon: 🛡️
    title: Field-Level Security
    details: Declare allowed, blocked, filterable, sortable, and selectable fields per-endpoint. Wildcard support, depth limits, and role-based access.
  - icon: ✅
    title: Built-in Validation
    details: Validates field paths, operators, types, and access rules before execution. Returns structured errors or throws — your choice.
  - icon: ⚡
    title: EF Core Ready
    details: Expression-tree based. Every operation translates to SQL — no client-side evaluation. Full async support via FlexQueryAsync (requires FlexQuery.NET.EntityFrameworkCore).
  - icon: 🧩
    title: Composable Pipeline
    details: Call individual steps (ApplyFilter, ApplySort, ApplyPaging, ApplySelect) or use the unified FlexQueryAsync for the complete pipeline (requires EF Core or Dapper provider).
---

<div class="home-content">

## What is FlexQuery.NET?

FlexQuery.NET is a **query abstraction layer** for .NET APIs.

It sits between your controller and your `IQueryable` and translates client-provided query parameters into safe, validated, server-side expressions.

It is **not** just a filtering helper. It handles the full query lifecycle:

```
HTTP Request → Parse → Validate → Filter → Sort → Page → Project → JSON Response
```

---

## A 60-Second Example

**The client sends this HTTP request:**

```
GET /api/customers?filter=status:eq:active&sort=createdDate:desc&page=1&pageSize=10&select=id,name,email
```

**Your controller handles it in 3 lines:**

```csharp
using FlexQuery.NET.EntityFrameworkCore;

[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email", "status", "city" };
    });

    return Ok(result);
}
```

> **Note:** `FlexQueryAsync` requires the `FlexQuery.NET.EntityFrameworkCore` or `FlexQuery.NET.Dapper` package. The core `FlexQuery.NET` package provides synchronous `FlexQuery` for advanced scenarios.

**The response:**

```json
{
  "data": [
    { "id": 1, "name": "Alice", "email": "alice@example.com" },
    { "id": 2, "name": "Bob",   "email": "bob@example.com" }
  ],
  "totalCount": 48,
  "resultCount": 48,
  "page": 1,
  "pageSize": 10
}
```

One endpoint. Full query power. Zero unsafe code.

---

## Choose Your Level of Control

FlexQuery.NET is designed to work at the level of abstraction that suits your use case.

| Level | Method | Use When |
| :--- | :--- | :--- |
| **High-level (recommended)** | `FlexQueryAsync` (EF Core/Dapper) or `FlexQuery` (Core) | You want parse + validate + execute in one call |
| **Mid-level** | `ToQueryOptions()` + manual pipeline | You need custom steps between parse and execute |
| **Low-level** | `ApplyFilter`, `ApplySort`, `ApplyPaging`, `ApplySelect` | You need full control over each pipeline stage |

---

## How the Execution Model Works

```
FlexQueryParameters (HTTP DTO)
          │
          ▼
   ToQueryOptions()           ← Parses to AST
          │
          ▼
      QueryOptions             ← The parsed model
          │
          │   (FlexQueryAsync includes validation automatically)
          │
          ├── ApplyFilter()              ← WHERE clause (expression tree)
          ├── ApplySort()                ← ORDER BY
          ├── ApplyPaging()              ← SKIP / TAKE
          ├── ApplyExpand()             ← Include pipeline (independent)
          └── ApplySelect()             ← Dynamic projection
                    │
                    ▼
             QueryResult<object>
      { data, totalCount, resultCount, page, pageSize }
```

`totalCount` is the number of filtered source records. `resultCount` is the number of shaped rows before paging, which differs for grouping, distinct projection, pivoting, or similar cardinality-changing operations.

All expression trees are translated to SQL by EF Core. No client-side evaluation.

---

## Supported Query Formats

| Format | Example |
| :--- | :--- |
| **DSL** | `filter=status:eq:active AND salary:gte:50000` |
| **FQL** | `filter=status = "active" AND salary >= 50000` |

---

## Package Overview

| Package | Purpose |
| :--- | :--- |
| `FlexQuery.NET` | Core library — filtering, sorting, paging, projection, validation. Provides synchronous `FlexQuery` method. |
| `FlexQuery.NET.EntityFrameworkCore` | EF Core async execution — `FlexQueryAsync`, `ApplyExpand`, `UseNoTracking` |
| `FlexQuery.NET.Dapper` | Dapper async execution — `FlexQueryAsync` for raw ADO.NET connections |
| `FlexQuery.NET.AspNetCore` | ASP.NET Core integration — `FieldAccessFilter`, `[FieldAccess]` attribute |

---

## Package Reference

Detailed documentation for each NuGet package:

| Package | README |
| :--- | :--- |
| `FlexQuery.NET` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) |
| `FlexQuery.NET.EntityFrameworkCore` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) |
| `FlexQuery.NET.Dapper` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) |
| `FlexQuery.NET.AspNetCore` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) |
| `FlexQuery.NET.Diagnostics` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Diagnostics/README.md) |
| `FlexQuery.NET.Adapters.AgGrid` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.AgGrid/README.md) |
| `FlexQuery.NET.Adapters.Kendo` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Adapters.Kendo/README.md) |
| `FlexQuery.NET.Parsers.FQL` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.FQL/README.md) |
| `FlexQuery.NET.Parsers.MiniOData` | [README](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) |

</div>
