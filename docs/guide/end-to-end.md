# End-to-End Example

## Overview

This example demonstrates the full lifecycle of a FlexQuery.NET request: tracing the execution path from the HTTP query string, through the AST parser and validation pipeline, into the final SQL generation, and ultimately returning the standardized JSON response.

## Why this feature exists

When introducing a new query engine to a team, developers often wonder "Where is the magic happening?". By tracing a single request end-to-end, you can clearly see that FlexQuery.NET does not perform in-memory evaluation. It is purely an AST-to-SQL compiler, ensuring your database does all the heavy lifting.

## When to use

- Share this page with your database administrators (DBAs) or backend engineers to prove that FlexQuery generates highly optimized, standard SQL queries with native pagination and parameterization.

---

## 1. The HTTP Request

A client wants to find all **Active** products in the **Electronics** category with a price greater than **$500**, sorted by the latest arrival. 

Notice how the `&` symbol combining filters is URL-encoded as `%26` so it doesn't collide with the standard HTTP query parameter separator.

```http
GET /api/products?filter=Status:eq:Active%26Category:eq:Electronics%26Price:gt:500&sort=CreatedAt:desc
```

---

## 2. The Controller Action

The request is automatically bound to a `FlexQueryParameters` DTO by ASP.NET Core and passed into the unified `FlexQueryAsync` pipeline.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters request)
{
    // 1. Parsing & Validation happens here
    // 2. IQueryable is extended with Expression Trees
    // 3. Query is executed against the DB
    var result = await _context.Products.FlexQueryAsync(request, options =>
    {
        // Enforce strict security: the client can only filter/sort these specific fields
        options.AllowedFields = ["Status", "Category", "Price", "CreatedAt", "Id", "Name"];
    });

    return Ok(result);
}
```

---

## 3. Generated SQL (EF Core)

FlexQuery translates the request AST into a single, optimized SQL query via Entity Framework Core. **No in-memory filtering occurs.**

```sql
SELECT [p].[Id], [p].[Name], [p].[Price], [p].[Status], [p].[Category], [p].[CreatedAt]
FROM [Products] AS [p]
WHERE (([p].[Status] = N'Active') 
  AND ([p].[Category] = N'Electronics')) 
  AND ([p].[Price] > 500.0)
ORDER BY [p].[CreatedAt] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
```

*(Note: In production, values like `N'Active'` and `500.0` are passed as DbParameters (`@p0`, `@p1`) to prevent SQL injection. They are shown as literals here for readability).*

---

## 4. The JSON Response

The client receives a structured response containing the requested data and the standard v4 pagination metadata envelope (`QueryResult<T>`).

```json
{
  "totalCount": 2,
  "resultCount": 2,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    {
      "id": 101,
      "name": "High-End Laptop",
      "price": 1200.0,
      "status": "Active",
      "category": "Electronics",
      "createdAt": "2026-05-01T10:00:00Z"
    },
    {
      "id": 105,
      "name": "4K Monitor",
      "price": 550.0,
      "status": "Active",
      "category": "Electronics",
      "createdAt": "2026-04-28T14:30:00Z"
    }
  ],
  "nextCursorToken": null
}
```

---

## Best Practices

- **Client Flexibility**: The client can change the price threshold or category without requiring any backend code changes, redeployments, or new DTOs.
- **Server Security**: The server enforces a strict `AllowedFields` whitelist. If a malicious user tries to probe for `?filter=InternalCost:gt:100`, the server immediately returns a `400 Bad Request` validation error, and the database is never touched.
- **Database Efficiency**: The generated query uses standard `OFFSET/FETCH` pagination (or `WHERE Id > cursor` if Keyset pagination is enabled) at the database level, meaning bandwidth and memory are preserved.
