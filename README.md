# DynamicQueryable

DynamicQueryable is a lightweight .NET library for applying **dynamic filtering, sorting, paging, and projection** to `IQueryable` (EF Core or any LINQ provider). It supports multiple query-string formats and produces **EF Core-translatable** expression trees.

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

- **Filtering**: nested AND/OR groups, nested property paths, collection paths (EXISTS/`Any`)
- **Sorting**: multi-field ordering
- **Paging**: `page` / `pageSize` or `skip` / `take` (format-dependent)
- **Projection**: `select` with nested properties, plus `include`-style expansion
- **Query formats**: Generic, JSON, DSL, JQL-lite, Syncfusion, Laravel Spatie
- **EF Core friendly**: expression-tree based, provider-translatable
- **Pluggable operators**: core ships framework-agnostic handlers, optional packages can override by operator

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

### JQL-lite (`query`)

Use SQL-like operators with `AND` / `OR` and parentheses for grouping:

```http
?query=(name = "john" OR name = "doe") AND age >= 20
```

Supports nested property paths and quoted values:

```http
?query=email = "ops@acmeretail.com" AND orders.number = "ORD-2026-0002" AND orders.items.quantity > 2
```

Supported JQL operators:

- `=` `!=` `>` `>=` `<` `<=`
- `CONTAINS`
- `IN (...)` and `NOT IN (...)`

Unlike DSL/JSON malformed-input handling, invalid JQL syntax is surfaced as a parse exception to callers.

### Syncfusion

```http
?where[0][field]=Name
&where[0][operator]=contains
&where[0][value]=john
&sorted[0][name]=Age
&sorted[0][direction]=descending
&skip=0
&take=10
```

Use `condition=and|or` for top-level logic.

### Laravel Spatie

**Implicit AND (default Spatie behavior)**

```http
?filter[name]=Alice Johnson
&filter[status]=Active
```

**Nested grouping**

```http
?filter[or][0][name]=john
&filter[or][1][name]=doe
```

**Explicit operator support (extension)**

```http
?filter[name][operator]=contains
&filter[name][value]=john
```

(Works inside nested `and/or` groups as well.)

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

## Nested & Collections

### Nested property paths

Dot-notation works across filtering and projection:

```http
?filter[0].field=Profile.Bio&filter[0].operator=contains&filter[0].value=dev
&select=Id,Profile.Bio
```

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

## License

MIT License