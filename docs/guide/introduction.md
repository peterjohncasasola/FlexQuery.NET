# Introduction

## Overview

FlexQuery.NET is a lightweight, high-performance .NET library that enables **dynamic filtering, sorting, paging, grouping, and projection** over any `IQueryable` (Entity Framework Core) or `IDbConnection` (Dapper). 

Instead of writing dozens of custom API endpoints or complex `switch` statements to handle optional filters, FlexQuery.NET translates incoming HTTP query parameters directly into secure, EF Core-translatable expression trees or optimized, parameterized raw SQL strings.

## Why this feature exists

Building a data-rich UI (like an enterprise data grid or dashboard) requires extreme flexibility from the backend. Developers typically encounter a cross-roads where they either:
1. **Hardcode hundreds of filter combinations**: Writing a massive `/api/orders?status=x&minTotal=y&date=z` endpoint with manual `if (request.HasStatus)` branches, which becomes unmaintainable.
2. **Adopt heavy frameworks**: Using [GraphQL](/guide/comparison) or [OData](/guide/comparison), which introduce steep learning curves, entirely new protocol specs, and significant operational overhead.

**FlexQuery.NET provides the sweet spot:** It offers the deep flexibility of GraphQL and OData, but it stays within the standard REST paradigm without requiring you to radically alter your backend architecture.

## When to use

- You are building internal dashboards, admin panels, or data grids (e.g., AG Grid, Syncfusion, Kendo UI, Vue/React tables) where users need to dynamically slice and dice data.
- You need a highly flexible API but don't want the overhead or complexity of [OData](/guide/comparison) or [GraphQL](/guide/comparison).
- You require strong, declarative security (Field-Level Security) to ensure clients cannot probe restricted data (e.g. `?filter=passwordHash:eq:xxx`).
- You are using Entity Framework Core or Dapper as your data access layer.

## When not to use

- **Rigid CQRS Systems:** If you strictly adhere to a Command Query Responsibility Segregation architecture where every read has a highly specialized, immutable DTO that clients cannot alter.
- **NoSQL Databases:** FlexQuery.NET translates to LINQ `IQueryable` and ADO.NET dialects. It is fundamentally designed for relational database querying and will not directly translate to Cosmos DB or MongoDB pipelines.

---

## Complete Runnable Example

FlexQuery.NET bridges the HTTP request directly to your database with an explicit, secure configuration layer.

**Backend (C#) using Minimal APIs:**
```csharp
app.MapGet("/api/orders", async (
    [AsParameters] FlexQueryParameters parameters, 
    AppDbContext dbContext) =>
{
    // Executes the query directly via EF Core
    var result = await dbContext.Orders.FlexQueryAsync(parameters, options => 
    {
        // Security boundary: Only allow access to these specific fields
        options.AllowedFields = ["Id", "Status", "Total", "CreatedAt"];
        
        // Performance boundary: Hard cap the number of items returned
        options.MaxPageSize = 200;
        options.DefaultPageSize = 50;
    });

    return Results.Ok(result);
});
```

## HTTP Request and JSON Response

**The Client Request:**
```http
GET /api/orders
    ?filter=Status:eq:Paid&Total:gt:500
    &sort=Total:desc
    &select=Id,Total
    &page=1
```

**The Server Response (`QueryResult<T>`):**
FlexQuery standardizes the payload into an envelope containing the data and pagination metadata.

```json
{
  "totalCount": 1500,
  "resultCount": 1500,
  "page": 1,
  "pageSize": 50,
  "totalPages": 30,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1042, "total": 1250.00 },
    { "id": 1089, "total": 850.50 }
  ],
  "nextCursorToken": null
}
```

---

## Performance Notes

FlexQuery.NET executes on the server; it does **not** evaluate expressions in memory. For EF Core, it dynamically constructs an `IQueryable` expression tree so that Entity Framework generates the optimized SQL query. For Dapper, it generates highly optimized, parameterized raw SQL strings specifically mapped to your target dialect (e.g., PostgreSQL, SQL Server). 

Furthermore, FlexQuery supports **Keyset Pagination**, allowing you to bypass expensive `OFFSET/FETCH` scanning for massive datasets.

## Security Notes

Client input is treated as inherently hostile. The `QueryOptions` parser acts only as an AST generator. Before the database is ever touched, the validation phase compares the AST against your server-side `AllowedFields` (the whitelist). If a client requests a field or relationship they are not permitted to see, FlexQuery halts immediately with a `QueryValidationException` (in strict mode).

## Best Practices

- **Always configure `AllowedFields`**: Never execute an unfiltered query options payload against your database. Always explicitly declare what fields the client is allowed to manipulate.
- **Always set `MaxPageSize`**: Prevent malicious clients from attempting to pull down millions of rows by setting a hard limit on page requests.
- **Use `CaseInsensitive = true`**: When configuring FlexQuery globally, enabling case insensitivity provides a friendlier developer experience for frontend teams passing URL parameters.

## Related Topics

- [Installation and Setup](/guide/getting-started#installation)
- [Filtering and Sorting Syntax](/guide/filtering)
- [Security and Governance](/guide/security)
- [FlexQuery vs GraphQL/OData](/guide/comparison)
