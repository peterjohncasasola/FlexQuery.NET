# Introduction

FlexQuery.NET is a lightweight, high-performance .NET library that enables **dynamic filtering, sorting, paging, grouping, and projection** over any `IQueryable` (EF Core or any LINQ provider).

## What it does

Instead of writing dozens of custom API endpoints or complex `switch` statements to handle optional filters, FlexQuery.NET translates incoming HTTP query parameters directly into secure, EF Core-translatable expression trees.

**Request:**
```http
GET /api/orders?filter=Status:eq:Paid&sort=Total:desc&select=Id,Total
```

**Backend (C#):**
```csharp
var options = QueryOptionsParser.Parse(Request.Query);
var orders = await _dbContext.Orders.ApplyValidatedQueryOptions(options).ToListAsync();
```

## Why it exists

Building a data-rich UI (like a data grid or dashboard) requires extreme flexibility from the backend. Developers typically either:
1. Hardcode hundreds of filter combinations (unmaintainable).
2. Adopt heavy frameworks like [GraphQL](/guide/comparison) or [OData](/guide/comparison) (overkill, steep learning curve).

**FlexQuery.NET provides the sweet spot:** The flexibility of GraphQL, with the simplicity of standard REST APIs.

## When to use it

- You are building internal dashboards, admin panels, or data grids (e.g., AG Grid, Syncfusion, Vue/React tables).
- You need a flexible API but don't want the overhead of [OData](/guide/comparison) or [GraphQL](/guide/comparison).
- You need strong security (Field-Level Security) to ensure clients can't probe restricted data.
