> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Include (Eager Loading)

FlexQuery.NET allows clients to request related entities to be returned alongside the parent entity, significantly reducing the N+1 problem and eliminating the need for multiple API requests.

Behind the scenes, this leverages EF Core's `.Include()` and `.ThenInclude()`.

## Basic Include

To load a related collection or navigation property, use the `select` parameter to list both primary fields and the nested navigation fields. Or, depending on your configuration, use the explicit `includes` mapping on `QueryRequest`.

**Example Request:**
Include the `Orders` collection.
```http
GET /api/customers?select=Id,Name,Orders.Id,Orders.Total
```

**Backend (C#):**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    var options = QueryOptionsParser.Parse(request);
    
    // Applying select automatically figures out which Includes are needed!
    var users = await _context.Customers
        .ApplyValidatedQueryOptions(options)
        .ToListAsync();

    return Ok(users);
}
```

## Deep Includes

You can navigate through multiple layers of relationships using dot notation.

**Example Request:**
Fetch Customers, their Orders, and the Order Items.
```http
GET /api/customers?select=Id,Orders.Id,Orders.Items.ProductId
```

## Scoped Filtering (Join with Filter)

A unique feature of FlexQuery.NET is the ability to apply filters directly to related collections within the `include` or `select` parameters. This allows you to fetch a parent entity and only a subset of its related children (e.g., only "Active" orders).

**Example Request:**
Fetch Customers and only their "Completed" orders.
```http
GET /api/customers?include=Orders(Status = 'Completed')
```

**How it works:**
When a filter is applied to a collection navigation, FlexQuery.NET automatically generates the necessary SQL **JOIN** or **EXISTS** clause. If you are using projection (`select`), it builds a filtered projection on the navigation property, ensuring that only the matching child records are materialized.


## Security Considerations

To prevent clients from including massive data graphs (which can cause denial of service), you should strongly consider using `MaxFieldDepth` to limit how deeply clients can nest their queries.

```csharp
// Limit nesting to 2 levels (e.g., Customer -> Orders -> Items)
options.MaxFieldDepth = 2;
```

