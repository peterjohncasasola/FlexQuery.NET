# Include (Eager Loading)

## Overview

FlexQuery.NET allows clients to request related entities to be returned alongside the parent entity, significantly reducing the N+1 query problem and eliminating the need for multiple API round trips.

Behind the scenes, this leverages EF Core's `.Include()` and `.ThenInclude()`.

## Why this feature exists

In REST APIs, it is common to either over-fetch (load everything in a massive graph) or under-fetch (make multiple roundtrips to load related data). FlexQuery's `include` parameter enables precise, client-controlled eager loading with optional inline filters, giving frontends the data they need in a single request.

## When to use

- Use `include` when the frontend needs related data alongside the parent entity in a single request.
- Use `include=Orders(Status:eq:Active)` when you want to conditionally load a subset of a collection.
- Use `MaxFieldDepth` to prevent clients from traversing unbounded object graphs.

---

## Basic Include

To load a related collection or navigation property, use the `include` parameter:

```http
GET /api/customers?include=Orders
```

To project specific fields from an included collection, use dot notation in the `select` parameter:

```http
GET /api/customers?select=Id,Name,Orders.Id,Orders.Total&include=Orders
```

**Backend (C#):**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, options =>
    {
        options.AllowedFields = ["Id", "Name", "Orders.Id", "Orders.Total"];
        options.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

---

## Deep Includes

You can navigate through multiple layers of relationships using dot notation. The `MaxFieldDepth` setting controls how deeply clients can traverse.

```http
GET /api/customers?select=Id,Orders.Id,Orders.Items.ProductId&include=Orders.Items
```

---

## Scoped Filtering (Filtered Include)

A powerful feature of FlexQuery.NET is the ability to apply filters directly to included collections. This allows you to fetch a parent entity and only a matching subset of its related children.

```http
GET /api/customers?include=Orders(Status:eq:Completed)
```

When a filter is applied to a collection navigation, FlexQuery.NET generates the EF Core `.Include(x => x.Orders.Where(o => o.Status == "Completed"))` expression. This means only matching child records are materialized.

> [!IMPORTANT]
> Filtered includes do **not** affect which root entities are returned. All customers are returned; only the included orders are filtered. See [Include Filtering](/guide/include-filtering) for a detailed comparison.

---

## Security Considerations

To prevent clients from including massive data graphs (which can cause denial of service), always configure `MaxFieldDepth` to limit how deeply clients can nest their queries.

```csharp
var result = await _context.Customers.FlexQueryAsync(parameters, options =>
{
    // Limit nesting to 2 levels (e.g., Customer → Orders → Items)
    options.MaxFieldDepth = 2;
    options.AllowedFields = ["Id", "Name", "Orders.Status"];
});
```

## Related Topics

- [Include Filtering](/guide/include-filtering) — Detailed guide on filtered includes vs root filter semantics
- [Security Governance](/guide/security-governance) — MaxFieldDepth and field access validation
