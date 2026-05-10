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
    details: Support for DSL, JQL, JSON, and indexed query-string formats. Nested AND/OR, collection predicates (any/all), and 20+ operators out of the box.
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
    details: Expression-tree based. Every operation translates to SQL — no client-side evaluation. Full async support via FlexQueryAsync.
  - icon: 🧩
    title: Composable Pipeline
    details: Call individual steps (ApplyFilter, ApplySort, ApplyPaging, ApplySelect) or use the unified FlexQueryAsync for the complete pipeline.
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
GET /api/users?filter=status:eq:active&sort=createdAt:desc&page=1&pageSize=10&select=id,name,email
```

**Your controller handles it in 3 lines:**

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email", "status", "createdAt" };
    });

    return Ok(result);
}
```

**The response:**

```json
{
  "data": [
    { "id": 1, "name": "Alice", "email": "alice@example.com" },
    { "id": 2, "name": "Bob",   "email": "bob@example.com" }
  ],
  "totalCount": 48,
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
| **High-level (recommended)** | `FlexQueryAsync` | You want parse + validate + execute in one call |
| **Mid-level** | `QueryOptionsParser.Parse` + manual pipeline | You need custom steps between parse and execute |
| **Low-level** | `ApplyFilter`, `ApplySort`, `ApplyPaging`, `ApplySelect` | You need full control over each pipeline stage |

---

## How the Execution Model Works

```
FlexQueryParameters (HTTP DTO)
         │
         ▼
  QueryOptionsParser.Parse()
         │
         ▼
     QueryOptions (AST)
         │
         ├── ValidateOrThrow<T>()       ← Field access, operator, type checks
         │
         ├── ApplyFilter()              ← WHERE clause (expression tree)
         ├── ApplySort()                ← ORDER BY
         ├── ApplyPaging()              ← SKIP / TAKE
         ├── ApplyFilteredIncludes()    ← Include pipeline (independent)
         └── ApplySelect()             ← Dynamic projection
                   │
                   ▼
            QueryResult<object>
         { data, totalCount, page, pageSize }
```

All expression trees are translated to SQL by EF Core. No client-side evaluation.

---

## Supported Query Formats

FlexQuery.NET auto-detects the input format:

| Format | Example |
| :--- | :--- |
| **DSL** | `filter=status:eq:active` |
| **JQL** | `query=status = "active" AND age >= 18` |
| **JSON** | `filter={"logic":"and","filters":[{"field":"status","operator":"eq","value":"active"}]}` |
| **Indexed** | `filter[0].field=status&filter[0].operator=eq&filter[0].value=active` |

---

## Package Overview

| Package | Purpose |
| :--- | :--- |
| `FlexQuery.NET` | Core library — filtering, sorting, paging, projection, validation |
| `FlexQuery.NET.EFCore` | EF Core async execution — `FlexQueryAsync`, `ApplyFilteredIncludes` |
| `FlexQuery.NET.AspNetCore` | ASP.NET Core integration — `FieldAccessFilter`, `[FieldAccess]` attribute |

</div>
