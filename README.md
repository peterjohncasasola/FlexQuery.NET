<p align="center">
  <img src="assets/logo.png" width="200"/>
</p>

<h1 align="center">FlexQuery.NET</h1>

<p align="center">
  Dynamic filtering, sorting, pagination and projection for IQueryable in .NET
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/FlexQuery.NET">
    <img src="https://img.shields.io/nuget/v/FlexQuery.NET.svg" />
  </a>
  <a href="https://www.nuget.org/packages/FlexQuery.NET">
    <img src="https://img.shields.io/nuget/dt/FlexQuery.NET.svg" />
  </a>
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg" />
</p>

---

**FlexQuery.NET** is a lightweight and powerful .NET library that enables **dynamic filtering, sorting, paging, and projection** over `IQueryable` (EF Core or any LINQ provider).

It converts query parameters into **EF Core-translatable expression trees**, making it ideal for building flexible APIs without hardcoding queries.

## Installation

```bash
dotnet add package FlexQuery.NET
```

Optional (async helpers for EF Core):

```bash
dotnet add package FlexQuery.NET.EFCore
```

## Quick Start

FlexQuery.NET supports two distinct usage patterns. The **Simple Usage** is perfect for rapid prototyping, while the **Advanced Usage** using the `QueryRequest` DTO is highly recommended for production APIs requiring clean OpenAPI/Swagger bindings and strong separation between client input and server-side security.

### 1. Simple Usage (Direct Parsing)

Directly parse the raw `Request.Query`.

```csharp
using FlexQuery.NET;
using FlexQuery.NET.Parsers;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Parse raw query string into internal model
        var options = QueryOptionsParser.Parse(Request.Query);

        // Filter + sort + paging + projection
        var users = await _context.Users
            .ApplyValidatedQueryOptions(options)
            .ToListAsync();

        return Ok(users);
    }
}
```

### 2. Advanced Usage (Recommended)

> **💡 Tip:** Use the `QueryRequest` DTO to prevent malicious clients from overriding server-side security rules (like `AllowedFields`).

```csharp
using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] QueryRequest request)
    {
        // 1. Parse the safe DTO into the execution model
        var options = QueryOptionsParser.Parse(request);

        // 2. Apply strict server-side security
        options.MaxFieldDepth = 3;
        options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            "Id", "Name", "Email", "Orders.*" 
        };

        // 3. Execution (validation happens implicitly)
        var users = await _context.Users
            .ApplyValidatedQueryOptions(options)
            .ToListAsync();

        return Ok(users);
    }
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
- **Validation Engine**: Pre-execution query validation (field existence, operator validity, type safety)
- **Field-Level Security**: Built-in whitelisting and blacklisting of fields (supports nested paths)


## 🔄 Migration from DynamicQueryable.Extensions

FlexQuery.NET is the successor of DynamicQueryable.Extensions.

### Key Improvements
- Rewritten architecture for better performance and extensibility
- Cleaner and more consistent API surface
- Enhanced EF Core integration
- Support for multiple query formats (DSL, JSON, Indexed, JQL)

> ⚠️ Old versions and changelog history are not carried over to maintain a clean versioning strategy.


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

FlexQuery.NET parses incoming query parameters into a unified model (`QueryOptions`, `FilterGroup`, `FilterCondition`). Operator behavior is consistent across formats.

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

FlexQuery.NET implements a **dual-pipeline** architecture to solve the "over-filtering" problem. It allows you to filter which root entities are returned (WHERE) independently from how their related collections are shaped (Filtered Includes).

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
using FlexQuery.NET;
using FlexQuery.NET.EFCore;

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
using FlexQuery.NET;

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

### EF Core async helpers (package: `FlexQuery.NET.EFCore`)

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
using FlexQuery.NET.EFCore;

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

## Production Readiness: Security & Performance

`FlexQuery.NET` is designed with enterprise-grade security and performance in mind, ensuring it can be safely exposed to public APIs.

### Security Hardening & SQL Injection Protection

The library implements multiple layers of security to prevent malicious queries:

1. **Strict Parameterization:**
   All user-provided inputs (strings, numbers, dates) are automatically handled as safe parameterized constants. EF Core translates these securely into parameterized SQL (e.g., `@p0`). No raw SQL interpolation is used.
2. **Fail-Fast Validation (JQL / DSL):**
   The query parsers strictly enforce syntax validation. Any dangerous tokens (like `;`, `--`, `DROP`, or `UNION`) result in an immediate `JqlParseException`, stopping execution before it ever reaches EF Core.
3. **Whitelist-Based Execution:**
   - **Operators:** Only whitelisted operators (e.g., `eq`, `contains`, `any`) are supported via the internal `OperatorRegistry`.
   - **Fields:** Properties and paths are strictly validated against your actual EF Core models. Non-existent or protected fields are rejected.
4. **Alias Validation:**
   Dynamic projections map user-provided aliases via strict regex (`^[a-zA-Z0-9_]+$`), neutralizing projection-based injection mapping attempts.

### Performance & Query Optimization

When exposing queries over large datasets, performance is critical:

- **Optimized Paging:** When paging (`skip`/`take`) is requested without an explicit sort, the library automatically injects a default `OrderBy` (using `Id` or the primary key) to prevent EF Core errors on relational databases and ensure deterministic results.
- **Efficient EXISTS Translation:** Deeply nested filters like `orders.any(orderItems.any(quantity > 5))` are efficiently translated by EF Core into nested SQL `EXISTS` clauses without fetching intermediate records into memory.
- **Memory Optimization:** Filtering is strictly applied *before* any dynamic projection, reducing the memory footprint of materialized objects.

### Monitoring & Logging

We recommend leveraging EF Core's built-in Interceptors alongside ASP.NET Core Middleware to trace dynamic queries.

#### 1. SQL Query Logging (EF Core Interceptor)
You can create an EF Core Interceptor to track slow dynamic queries and log execution metrics:

```csharp
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

public class QueryPerformanceInterceptor : DbCommandInterceptor
{
    private readonly ILogger _logger;
    public QueryPerformanceInterceptor(ILogger logger) => _logger = logger;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (eventData.Duration.TotalMilliseconds > 500)
        {
            _logger.LogWarning("Slow Dynamic Query Executed ({Duration}ms):\n{CommandText}", eventData.Duration.TotalMilliseconds, command.CommandText);
        }
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}

// Program.cs
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(new QueryPerformanceInterceptor(logger));
});
```

#### 2. Raw Request Logging (ASP.NET Core Middleware)
To track incoming dynamic requests, you can add a simple request logging middleware:

```csharp
app.Use(async (context, next) =>
{
    if (context.Request.Query.ContainsKey("query"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Dynamic Query Request: {QueryString}", context.Request.QueryString.Value);
    }
    await next(context);
});
```

### 🛡️ Query Validation Engine

FlexQuery.NET provides a pluggable validation engine that inspects `QueryOptions` before they are applied to an `IQueryable`. This prevents invalid queries (e.g., non-existent fields, incompatible types) from causing runtime exceptions or database errors.

#### Using `ApplyValidatedQueryOptions`
The easiest way to use the validation engine is via the `ApplyValidatedQueryOptions` extension method. It validates the options and applies them in a single step, throwing a `QueryValidationException` if any rules are violated.

```csharp
using FlexQuery.NET;

try
{
    var users = await _context.Users
        .ApplyValidatedQueryOptions(options)
        .ToListAsync();
}
catch (QueryValidationException ex)
{
    // Handle validation errors (ex.Result.Errors)
    return BadRequest(ex.Result.Errors);
}
```

#### Manual Validation
You can also run the validation manually if you need to inspect the results before taking action:

```csharp
using FlexQuery.NET.Validation;

var validator = new QueryValidator();
var result = validator.Validate<User>(options);

if (!result.IsValid)
{
    var errors = result.Errors;
    // ...
}
```

### 🛡️ Field-Level Security (Whitelisting / Blacklisting)

FlexQuery.NET includes a built-in security rule to restrict access to specific fields. This is integrated into the validation pipeline.

#### Whitelisting (AllowedFields)
Only fields in this list can be queried. Any attempt to filter, sort, or select a field not in this list will throw a `QueryValidationException`.

```csharp
var options = QueryOptionsParser.Parse(Request.Query);
options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
{ 
    "Id", "Name", "Email", "Orders.Status", "Orders.Total" 
};

// Throws if disallowed fields are used
query.ApplyValidatedQueryOptions(options);
```

#### Blacklisting (BlockedFields)
Fields in this list are explicitly forbidden. This is useful for hiding sensitive data like SSNs or internal flags.

```csharp
options.BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
{ 
    "SSN", "PasswordHash", "InternalMetadata" 
};
```

### 🌐 ASP.NET Core Integration (`FlexQuery.NET.AspNetCore`)

For ASP.NET Core applications, you can use the `FlexQuery.NET.AspNetCore` package to apply security declaratively via attributes.

#### 1. Registration
Register the security filters in your `Program.cs`:

```csharp
using FlexQuery.NET.AspNetCore.Extensions;

// For Controllers
builder.Services.AddControllers()
    .AddFlexQuerySecurity();

// OR for Minimal APIs / Web API
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FieldAccessFilter>();
});
```

#### 2. Declarative Security
Use the `[FieldAccess]` attribute on controllers or actions:

```csharp
[ApiController]
[Route("api/[controller]")]
[FieldAccess(Allowed = new[] { "Id", "Name", "Email" })] // Controller-level whitelist
public class UsersController : ControllerBase
{
    [HttpGet]
    [FieldAccess(Allowed = new[] { "Orders.*" })] // Action-level additional fields
    public async Task<IActionResult> Get([FromQuery] QueryOptions options)
    {
        // Settings from attributes are automatically merged into 'options'
        return Ok(await _context.Users.ApplyValidatedQueryOptions(options).ToListAsync());
    }
}
```

> [!IMPORTANT]
> - **Wildcards**: Supports wildcards (e.g., `Orders.*` allows all sub-properties of Orders).
> - **Custom Resolvers**: Use `IFieldAccessResolver` for complex logic (e.g., role-based).
> - **Nested Support**: Security rules respect nested paths (e.g., `Orders.Status`).
> - **Casing**: The library uses the **canonical property names** (from your C# model) when checking against the whitelist/blacklist, ensuring protection regardless of the casing used in the query string.

## 🔍 Query Debug Mode

FlexQuery.NET provides a powerful debug mode to inspect the transformation from string-based queries to LINQ Expression Trees. This is essential for troubleshooting complex nested queries or verifying EF Core translation.

### Usage

```csharp
using FlexQuery.NET;

var options = QueryOptionsParser.Parse(Request.Query);
var debug = _context.Customers.ToFlexQueryDebug(options);

// Inspect the results
Console.WriteLine(debug.LinqLambda);      // The C#-like LINQ syntax
Console.WriteLine(debug.ExpressionTree);  // The structural node tree
Console.WriteLine(debug.Ast);             // The raw parsed AST (JQL/DSL)
```

### Example Output
For a query like `?query=orders.any(status = Cancelled AND orderItems.id = 101)`:

**LINQ Lambda:**
```csharp
query.Where(x => x.Orders.Any(sc => (sc.Status == "Cancelled") && sc.OrderItems.Any(i => i.Id == 101)))
```

**AST (ToString):**
```text
orders.any(AND(status eq [Cancelled], orderItems.any(id eq [101])))
```

## ⚖️ License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.
## Security (Field-Level Access Control)

Never expose your entire database schema. FlexQuery.NET provides a robust, pluggable security pipeline that runs *before* database execution.

When using QueryOptionsParser.Parse(request), configure your rules on the resulting QueryOptions:

`csharp
// 1. Whitelisting (Only allow these fields to be touched)
options.AllowedFields = new HashSet<string> { "Id", "Name", "Orders.*" };

// 2. Blacklisting (Deny access to sensitive fields)
options.BlockedFields = new HashSet<string> { "PasswordHash", "SSN" };

// 3. Operation-Specific Rules
options.FilterableFields = new HashSet<string> { "Status", "CreatedAt" };
options.SortableFields = new HashSet<string> { "CreatedAt" };
options.SelectableFields = new HashSet<string> { "Id", "Name", "Status" };

// 4. Depth Protection
options.MaxFieldDepth = 3; // Prevent 'Orders.Items.Product.Category.Name'
`

If a client attempts to filter, sort, or select a restricted field, a QueryValidationException is thrown with detailed ValidationResult errors, preventing the query from ever hitting the database.

