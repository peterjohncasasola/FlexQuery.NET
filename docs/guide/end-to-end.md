# End-to-End Example

This example demonstrates the full lifecycle of a FlexQuery request: from the HTTP query string to the final SQL execution and JSON response.

## 1. The HTTP Request

A client wants to find all **Active** products in the **Electronics** category with a price greater than **$500**, sorted by the latest arrival.

```http
GET /api/products?filter=Status:eq:Active & Category:eq:Electronics & Price:gt:500 & sort=CreatedAt:desc
```

## 2. The Controller Action

The request is bound to a `FlexQueryParameters` DTO and processed through the validated pipeline.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters request)
{
    // 1. Parsing & Validation happens here
    // 2. IQueryable is extended with Expression Trees
    // 3. Query is executed against the DB
    var result = await _context.Products
        .ApplyValidatedQueryOptions(request) //Deprecated in v2.0
        .ToQueryResultAsync();

    return Ok(result);
}
```

## 3. Generated SQL (EF Core)

FlexQuery translates the request into a single, optimized SQL query. No in-memory filtering occurs.

```sql
SELECT [p].[Id], [p].[Name], [p].[Price], [p].[Status], [p].[Category], [p].[CreatedAt]
FROM [Products] AS [p]
WHERE ([p].[Status] = N'Active') 
  AND ([p].[Category] = N'Electronics') 
  AND ([p].[Price] > 500.0)
ORDER BY [p].[CreatedAt] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
```

## 4. The JSON Response

The client receives a structured response containing the requested data and pagination metadata.

```json
{
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
  "totalCount": 2,
  "page": 1,
  "pageSize": 20
}
```

## Why this is Powerful

- **Client Flexibility**: The client can change the price threshold or category without any backend changes.
- **Server Security**: The server enforces that only `Active` products are visible (if you added a hardcoded filter) and only allows filtering on valid fields.
- **Database Efficiency**: The query uses standard SQL indexes and performs pagination at the database level.


