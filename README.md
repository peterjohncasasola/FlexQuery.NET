# 🚀 DynamicQueryable.Extensions

[![NuGet Version](https://img.shields.io/nuget/v/DynamicQueryable.Extensions.svg)](https://www.nuget.org/packages/DynamicQueryable.Extensions)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DynamicQueryable.Extensions.svg)](https://www.nuget.org/packages/DynamicQueryable.Extensions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**DynamicQueryable.Extensions** is a lightweight and powerful .NET library that enables **dynamic filtering, sorting, paging, and projection** over `IQueryable` (EF Core or any LINQ provider).

It converts query parameters into **EF Core-translatable expression trees**, making it ideal for building flexible APIs without hardcoding queries.

## Installation

```bash
dotnet add package DynamicQueryable.Extensions
```

Optional (async helpers for EF Core):

```bash
dotnet add package DynamicQueryable.Extensions.EFCore
```

## Quick Start

### Parse request query into `QueryOptions`

```csharp
using DynamicQueryable.Parsers;

var options = QueryOptionsParser.Parse(Request.Query);
```

### Apply to `IQueryable`

```csharp
using DynamicQueryable.Extensions;
using Microsoft.EntityFrameworkCore;

[HttpGet]
public async Task<IActionResult> Get()
{
    var options = QueryOptionsParser.Parse(Request.Query);

    // Filter + sort + paging
    var users = await _context.Users
        .ApplyQueryOptions(options)
        .ToListAsync();

    // Projection (optional)
    var projected = await _context.Users
        .ApplyQueryOptions(options)
        .ApplySelect(options)
        .ToListAsync();

    return Ok(new { users, projected });
}
```

## Features

- **Filtering**: nested AND/OR groups, nested property paths, scoped collection filtering (`.any()`, `.all()`, `[...]`)
- **Sorting**: multi-field ordering
- **Paging**: `page` / `pageSize` or `skip` / `take` (format-dependent)
- **Projection**: `select` with nested properties, plus `include`-style expansion
- **Query formats**: DSL (primary), JSON (advanced), Indexed (compatibility), JQL fallback
- **EF Core friendly**: expression-tree based, provider-translatable
- **Pluggable operators**: core ships framework-agnostic handlers, optional packages can override by operator
- **Dual-Pipeline**: Decouples data filtering (WHERE) from data shaping (Filtered Includes)

### 🔽 Sorting
- **Basic**: `?sort=createdAt:desc`
- **Multi-field**: `?sort=createdAt:desc,total:asc`
- **Nested**: `?sort=customer.name:asc`
- **Aggregate**: `?sort=orders.sum(total):desc,orders.count():asc`

> [!NOTE]
> - Default direction is `asc`.
> - Dot notation is supported for nested properties.
> - **Aggregate Functions**: Supports `sum()`, `count()`, `max()`, `min()`, and `avg()` on collection paths.
> - **Direct Collection Sorting**: Sorting directly on a collection property (e.g., `orders.total`) is **NOT** supported; use an aggregate instead.


## Filtering & Query Formats

DynamicQueryable parses incoming query parameters into a unified model (`QueryOptions`, `FilterGroup`, `FilterCondition`). Operator behavior is consistent across formats.

### Generic (indexed)

**Simple filter**

```http
?filter[0].field=Name
&filter[0].operator=contains
&filter[0].value=john
```

**Top-level logic (AND/OR)**

```http
?logic=or
&filter[0].field=City&filter[0].operator=eq&filter[0].value=Berlin
&filter[1].field=City&filter[1].operator=eq&filter[1].value=Paris
```

### JSON (nested groups)

**Advanced nested logic**

```http
?filter={
  "logic":"and",
  "filters":[
    {
      "logic":"or",
      "filters":[
        {"field":"City","operator":"eq","value":"London"},
        {"field":"City","operator":"eq","value":"Berlin"}
      ]
    },
    {"field":"Age","operator":"between","value":"25,40"}
  ]
}
```

### DSL (compact filter string)

Use `&` for AND (URL-encode as `%26`), `|` for OR, parentheses for grouping:

```http
?filter=((city:eq:London|city:eq:Berlin)%26(age:between:25,40|status:eq:Pending))
```

DSL advanced operators:

```http
?filter=!name:eq:john
?filter=not(name:eq:john)
?filter=name:like:%john%
?filter=orders:any:total:gt:100
?filter=orders:count:gt:5
```

### Grouping, aggregates, and having

Use `group`, aggregate functions in `select`, and `having` for post-aggregation filtering:

```http
?group=category,status
&select=category,sum(total),count(id)
&having=sum(total):gt:10000
```

Supported aggregate functions in `select`/`having`:

- `sum(field)`
- `count(field)`
- `avg(field)`

### JQL-lite fallback (`query`)

Use SQL-like operators with `AND` / `OR` and parentheses for grouping:

```http
?query=(name = "john" OR name = "doe") AND age >= 20
```

Supports nested property paths and quoted values:

```http
?query=email = "ops@acmeretail.com" AND orders.number = "ORD-2026-0002" AND orders.items.quantity > 2
```

**Scoped collection filtering** (conditions apply to the same element):

```http
?query=orders.any(status = Cancelled AND total > 500)
?query=orders[status = Cancelled AND orderItems.any(id = 101)]
```

Supported JQL operators:

- `=` `!=` `>` `>=` `<` `<=`
- `CONTAINS`
- `IN (...)` and `NOT IN (...)`
- `IS NULL` and `IS NOT NULL`
- `BETWEEN ... AND ...`
- `LIKE`, `STARTSWITH`, `ENDSWITH`
- Collection predicates: `ANY`, `ALL`, `COUNT`

Unlike DSL/JSON malformed-input handling, invalid JQL syntax is surfaced as a parse exception to callers.

## Migration Notes (v2.0.0)

- Removed legacy format adapters: **Spatie** and **Syncfusion**
- Removed Syncfusion-style sorting support
- Supported formats are now **DSL**, **JSON**, **Indexed**, and **JQL fallback**
- If you were using Spatie/Syncfusion query strings, migrate requests to DSL or Indexed format

## Operators

| Operator     | Description                  | Example                          |
| ------------ | ---------------------------- | -------------------------------- |
| `eq`         | Equal                        | `Name eq 'John'`                 |
| `neq`        | Not equal                    | `Age neq 30`                     |
| `gt`         | Greater than                 | `Age gt 18`                      |
| `gte`        | Greater than or equal        | `Age gte 18`                     |
| `lt`         | Less than                    | `Age lt 60`                      |
| `lte`        | Less than or equal           | `Age lte 60`                     |
| `contains`   | String contains              | `Name contains 'jo'`             |
| `startswith` | String starts with           | `Name startswith 'Jo'`           |
| `endswith`   | String ends with             | `Name endswith 'hn'`             |
| `in`         | Value exists in a list       | `Status in ['Active','Pending']` |
| `notin`      | Value does not exist in list | `Status notin ['Inactive']`      |
| `between`    | Inclusive range              | `Age between 18,60`              |
| `isnull`     | Is null                      | `DeletedAt isnull`               |
| `notnull`    | Is not null                  | `DeletedAt notnull`              |
| `like`       | SQL LIKE pattern             | `Name like %john%`               |
| `any`        | Collection element predicate | `Orders any Total gt 100`        |
| `count`      | Collection count compare     | `Orders count gt 5`              |
| `!` / `not()`| Negates condition/group      | `!Name eq John`                  |
| `:`          | DSL operator separator       | `Status:eq:Cancelled` (inside include) |

## Nested & Collections

### Nested property paths

Dot-notation works across filtering and projection:

```http
?filter[0].field=Profile.Bio&filter[0].operator=contains&filter[0].value=dev
&select=Id as customerId,Profile.Bio as biography
```

### Aliases (`as`)
You can rename properties in the output dynamic object using the `as` keyword. This works at any level of nesting:

```http
?select=id as customerId, name, orders.status as orderStatus, orders.orderItems.productName as product
```

In the resulting object:
- `Id` becomes `customerId`
- `Orders.Status` becomes `orderStatus` inside each order
- `Orders.OrderItems.ProductName` becomes `product` inside each order item

### Collection paths (parent filtering)

Filtering on a collection navigation (e.g. `Orders.Number`) uses `Any(...)` / EXISTS semantics for the parent:

```http
?filter[0].field=Orders.Number
&filter[0].operator=eq
&filter[0].value=SO-001
```

Conceptually:

```csharp
x => x.Orders.Any(o => o.Number == "SO-001")
```

### Scoped Collection Filtering (JQL)

By default, independent conditions on a collection are interpreted as separate `Any()` checks. Scoped filtering ensures multiple conditions apply to the **same element**.

| Syntax | Description |
|---|---|
| `orders.any(...)` | Conditions apply to the same order |
| `orders.all(...)` | All orders must satisfy the inner filter |
| `orders[...]` | Shorthand for `orders.any(...)` |

**Conceptually:**

```http
?query=orders.any(status = Cancelled AND total > 500)
```

Translates to:

```csharp
x => x.Orders.Any(o => o.Status == "Cancelled" && o.Total > 500)
```

**Nested Scoped Filtering:**

```http
?query=orders.any(status = Cancelled AND orderItems.any(id = 101))
```

This ensures the `orderItems` condition is checked against items belonging to a `Cancelled` order. Scoped filters can be nested recursively to any depth.

> [!TIP]
> **Null Safety**: Collection access in scoped filters is automatically null-safe (e.g., `orders != null && orders.Any(...)`), preventing `NullReferenceException` when evaluating against in-memory collections or LINQ-to-Objects providers.


### Filtered child collections (when selected)

When a request **filters on a collection path** and that collection is **also projected**, the returned child collection is filtered to match the same criteria (projection-based, EF Core translatable):

```http
?filter[0].field=Orders.Number&filter[0].operator=eq&filter[0].value=SO-001
&select=Id,Orders.Number
```

Conceptually:

```csharp
x => new {
  x.Id,
  Orders = x.Orders
    .Where(o => o.Number == "SO-001")
    .Select(o => new { o.Number })
    .ToList()
}
```

## Dual-Pipeline Query System (EF Core)

DynamicQueryable implements a **dual-pipeline** architecture to solve the "over-filtering" problem. It allows you to filter which root entities are returned (WHERE) independently from how their related collections are shaped (Filtered Includes).

> [!TIP]
> **Unified Projection Mode**: When using `ApplySelect` or `ToProjectedQueryResultAsync`, the library automatically merges **Filtered Includes** and **Select** into a single optimized `Select()` expression. This ensures only requested columns are fetched and related data is filtered at the database level.

### Pipeline 1: Root Filtering (WHERE)
Filters which root entities appear in the results.
```http
?query=orders.any(status = 'Cancelled' AND total > 500)
```

### Pipeline 2: Data Shaping (Filtered Includes)
Filters the content of the included child collections without affecting the root entity count.
```http
?include=orders(total > 100).items(sku = 'SKU-BBB')
```

**Mixed Formats & Chains**:
Includes support chained segments and both DSL/JQL formats:
```http
?include=orders(status:eq:cancelled).items(sku = 'SKU-AAA')
```

**Exclusive Selection**:
If you provide a specific `select` path for a navigation, the library will **only** project those fields, overriding the default "include all scalars" behavior:
```http
?include=orders(total > 100)&select=id,orders.number
```
*(Result: Orders will have only ID and Number, filtered by total > 100)*

### Applying Both Pipelines
To use both, chain `ApplyQueryOptions` (for the WHERE/Sort/Paging pipeline) and `ApplyFilteredIncludes` (for the Include pipeline).

```csharp
using DynamicQueryable.Extensions;
using DynamicQueryable.Extensions.EFCore;

var options = QueryOptionsParser.Parse(Request.Query);

var results = await _context.Customers
    .AsNoTracking()
    .ApplyQueryOptions(options)      // Pipeline 1: Root WHERE
    .ApplyFilteredIncludes(options)  // Pipeline 2: Filtered Includes
    .ToListAsync();
```

> [!IMPORTANT]
> **Independence**: Filters in the `include` parameter **only** affect the shape of the related data. They do not filter the root entities. If you want to filter root entities based on collection criteria, use the `query` or `filter` parameters (Pipeline 1).

## API Methods

### Apply filter/sort/paging

```csharp
using DynamicQueryable.Extensions;

var options = QueryOptionsParser.Parse(Request.Query);

var query = _context.Users.AsQueryable()
    .ApplyQueryOptions(options); // filter + sort + paging

var data = await query.ToListAsync();
```

### Apply projection (`select` / `include` / JSON select tree)

```csharp
var projected = await _context.Users
    .ApplyQueryOptions(options)
    .ApplySelect(options)
    .ToListAsync(); // IQueryable<object>
```

## Flattened Projections

By default, the library preserves the original object hierarchy during projection. However, you can optionally flatten deep nested collections into a table-like rowset using `SelectMany` expression chains.

### Default Nested Output (Before)
Without a flattening mode, the output reflects the entity structure:

```json
[
  {
    "id": 1,
    "name": "Alice",
    "orders": [
      {
        "status": "Shipped",
        "orderItems": [
          { "product": "Laptop", "qty": 1 },
          { "product": "Mouse", "qty": 2 }
        ]
      }
    ]
  }
]
```

### 1. Flat Mode (`mode=flat`)
Linearizes a single navigation path into a flat list of leaf objects. This is ideal for reporting on deeply nested items where parent context is not required.

**Query:**
`?select=orders.orderItems.productName as product,orders.orderItems.quantity as qty&mode=flat`

**Output:**
```json
[
  { "product": "Laptop", "qty": 1 },
  { "product": "Mouse", "qty": 2 }
]
```

**Generated LINQ:**
The library builds a sequential `SelectMany` chain:
```csharp
query.SelectMany(c => c.Orders)
     .SelectMany(o => o.OrderItems)
     .Select(oi => new { product = oi.ProductName, qty = oi.Quantity })
```

> [!NOTE]
> **Constraint**: `mode=flat` requires a single linear navigation path. Branching into multiple collections or mixing root fields with deep collections will trigger a validation error.

### 2. Flat-Mixed Mode (`mode=flat-mixed`)
Flattens root entity fields alongside deeply nested collection fields into a single rowset. This mode preserves parent context by using correlated `SelectMany` projections.

**Query:**
`?select=id as customerId,name,orders.status as orderStatus,orders.orderItems.productName as product&mode=flat-mixed`

**Output:**
```json
[
  { "customerId": 1, "name": "Alice", "orderStatus": "Shipped", "product": "Laptop" },
  { "customerId": 1, "name": "Alice", "orderStatus": "Shipped", "product": "Mouse" }
]
```

**Generated LINQ:**
The library carries context through the chain using a progressive anonymous type:
```csharp
query.SelectMany(c => c.Orders, (c, o) => new { c, o })
     .SelectMany(x => x.o.OrderItems, (x, oi) => new {
         customerId = x.c.Id,
         name = x.c.Name,
         orderStatus = x.o.Status,
         product = oi.ProductName
     })
```
This mode allows you to "join" all levels of a hierarchy into a flat result set while maintaining full EF Core server-side translation.

## Example Request and Response

Here is how a real-world request looks when combining JQL filtering, selective projection, and filtered includes.

### Request
```http
GET /api/customers?query=(name contains "Connelly")&pageSize=2&select=id,email,name,orders.id,orders.orderDate,orders.status,orders.orderItems&include=orders(status = "cancelled").orderItems(productName = "Tasty Metal Pants")
```

### Response
```json
{
  "totalCount": 1,
  "page": 1,
  "pageSize": 2,
  "totalPages": 1,
  "data": [
    {
      "id": 42,
      "name": "John Connelly",
      "email": "j.connelly@example.com",
      "orders": [
        {
          "id": 1001,
          "orderDate": "2026-04-15T10:30:00Z",
          "status": "cancelled",
          "orderItems": [
            {
              "id": 5001,
              "productId": 101,
              "productName": "Tasty Metal Pants",
              "unitPrice": 49.99
            }
          ]
        }
      ]
    }
  ]
}
```

In this example:
- The **root filter** (`query`) limits results to customers whose name contains "Connelly".
- The **projection** (`select`) ensures we only fetch specific fields for the customer and their orders.
- The **filtered include** (`include`) ensures that only "cancelled" orders are returned, and within those orders, only the items matching "Tasty Metal Pants" are included in the collection.

### Return results with metadata

```csharp
var result = _context.Users.ToQueryResult(options);
// result.Data, result.TotalCount, result.Page, result.PageSize
```

### Return projected results with metadata

```csharp
var result = _context.Users.ToProjectedQueryResult(options);
// result.Data is List<object> shaped by Select/Includes/JSON select tree
```

### EF Core async helpers (package: `DynamicQueryable.Extensions.EFCore`)

```csharp
var result = await _context.Users.ToQueryResultAsync(options, cancellationToken);
var projected = await _context.Users.ToProjectedQueryResultAsync(options, cancellationToken);
```

### EF Core operator overrides (optional)

Core does not depend on EF Core. By default, `like` is handled with a framework-agnostic fallback:

- `%value%` -> `Contains`
- `%value` -> `EndsWith`
- `value%` -> `StartsWith`

When using the EF Core package, opt in to EF-specific operator handlers:

```csharp
using DynamicQueryable.Extensions.EFCore;

var options = QueryOptionsParser.Parse(Request.Query)
    .UseEfCoreOperators(); // registers EF.Functions.Like handler

var data = await _context.Users
    .ApplyQueryOptions(options)
    .ToListAsync();
```

## ASP.NET Integration (optional)

You can parse directly in controllers/minimal APIs:

```csharp
public abstract class BaseController : ControllerBase
{
    protected QueryOptions Options => QueryOptionsParser.Parse(Request.Query);
}
```

## ⚖️ License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.