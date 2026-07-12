# Entity Framework Core Provider

## Overview

`FlexQuery.NET.EntityFrameworkCore` is the primary execution engine for applications using Entity Framework Core. It extends FlexQuery's core `IQueryable` operations with EF-specific capabilities: async materialization, filtered includes via `Include()`/`ThenInclude()`, `AsNoTracking()` support, native aggregation translation, and SQL preview tools.

### What It Is

The EF Core provider takes the same `QueryOptions` AST that all FlexQuery packages share and executes it against an EF Core `IQueryable<T>`. It produces `QueryResult<object>` containing the paged, filtered, sorted, and optionally projected data.

### Why It Exists

While FlexQuery's core library builds LINQ expression trees, EF Core requires specific treatment for:
- **Async materialization** — `ToListAsync()` and `CountAsync()` must come from EF Core
- **Filtered includes** — EF Core's `Include()` / `ThenInclude()` chain requires a specific call pattern
- **No-tracking queries** — Performance optimization for read-only APIs
- **SQL preview** — Inspecting the generated SQL before execution
- **Aggregation** — EF Core translates `GroupBy` + aggregate projections into SQL `GROUP BY` with aggregate functions

### When to Use It

- You use EF Core as your primary data access layer
- You want automatic change tracking, migrations, and the EF Core ecosystem
- Your entity model is already configured in a `DbContext`

### When NOT to Use It

- You use Dapper or raw ADO.NET — use `FlexQuery.NET.Dapper` instead
- You need to query views, stored procedures, or CTEs that don't map cleanly to EF Core entities

## Installation

```bash
dotnet add package FlexQuery.NET.EntityFrameworkCore
```

## Basic Example

```csharp
[HttpGet("customers")]
public async Task<IActionResult> GetCustomers(
    [FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "CreatedAt" };
        opts.MaxPageSize = 100;
    });

    return Ok(result);
}
```

## The IQueryable Pipeline

When `FlexQueryAsync` is called, the following pipeline executes:

```
IQueryable<T> (your DbSet)
     │
     ▼ AsNoTracking() — if UseNoTracking is true
     │
     ▼ ApplyFilter() — WHERE clause from QueryOptions.Filter
     │
     ▼ ApplySort() — ORDER BY clause from QueryOptions.Sort
     │
     ▼ CountAsync() — Total count (if IncludeCount is true)
     │
     ▼ Aggregate query — Grand totals (if Aggregates without GroupBy)
     │
     ▼ ApplyPaging() — OFFSET/FETCH via Skip/Take
     │
     ▼ ApplyExpand() — EF Core Include/ThenInclude chain
     │
     ├── HasProjection? ──Yes──► ApplySelect() → ToListAsync() → Projected results
     │
     └── No projection ──► ToListAsync() → Full entity results
     │
     ▼
QueryResult<object>
```

## Filtered Includes

FlexQuery supports **filtered navigation property loading** — loading related data with inline conditions. This maps to EF Core's filtered `Include()`:

```
?include=Orders(Status = 'Active').Items(Quantity > 5)
```

This generates:
```csharp
query
    .Include(c => c.Orders.Where(o => o.Status == "Active"))
    .ThenInclude(o => o.Items.Where(i => i.Quantity > 5))
```

### Using ApplyExpand

If you need to apply includes separately from the main query:

```csharp
var options = parameters.ToQueryOptions();

var result = await _context.Customers
    .ApplyFilter(options)              // WHERE pipeline
    .ApplySort(options)                // ORDER BY
    .ApplyExpand(options)              // INCLUDE pipeline
    .ToListAsync();
```

**Important:** The Include pipeline is **independent** of the WHERE pipeline. Include filters control which related entities are loaded, not which root entities are returned.

## Projection

When `$select` is specified, FlexQuery builds a dynamic LINQ `Select()` expression that only queries the requested columns:

```
?select=Id,Name,Email

// EF Core translates to:
// SELECT [c].[Id], [c].[Name], [c].[Email] FROM [Customers] AS [c]
```

For nested projections with includes:
```
?select=Id,Name,Orders.Id,Orders.Total

// Generates a projection that creates anonymous types with nested collections
```

## Aggregations

FlexQuery supports grand total aggregation via LINQ:

```csharp
using FlexQuery.NET.Models.Aggregates;

var options = parameters.ToQueryOptions();
options.Aggregates = new List<AggregateModel>
{
    new() { Function = AggregateFunction.Sum, Field = "Price", Alias = "priceSum" },
    new() { Function = AggregateFunction.Avg, Field = "Price", Alias = "priceAvg" },
    new() { Function = AggregateFunction.Count, Field = "Id", Alias = "idCount" }
};

var result = await _context.Customers.FlexQueryAsync<Customer>(options);

// result.Aggregates:
// { "Price": { "sum": 15000, "avg": 250 }, "Id": { "count": 60 } }
```

When `GroupBy` is specified alongside aggregates, the pipeline uses `GroupByBuilder` to generate grouped aggregation queries that EF Core translates into SQL `GROUP BY` with aggregate functions.

## EF Core Query Options

The EF Core provider introduces `EfCoreQueryOptions`, which allows you to control query compilation behavior per-request without modifying the underlying `DbContext`.

```csharp
var result = await _context.Customers.FlexQueryAsync(parameters, opts =>
{
    opts.UseNoTracking = true;       // Adds .AsNoTracking()
});
```

### UseNoTracking
Disabling change tracking can improve query performance by 20-40% for large result sets because EF Core skips identity resolution and snapshot creation. Recommended for all read-only API endpoints.

### Split Query Optimization
When your query includes multiple collection navigations, EF Core normally generates a single massive SQL query with multiple `LEFT JOIN`s, which can cause a "cartesian explosion" of data transfer. Configure EF Core's native query splitting behavior on the `DbContext` level:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.UseSqlServer(connectionString, opt =>
        opt.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
}
```

> [!NOTE]
> The `UseSplitQuery` option was removed from FlexQuery options in v4. Use EF Core's native query splitting configuration instead.

### IgnoreAutoIncludes & IgnoreQueryFilters
These options are not available in FlexQuery v4. To bypass globally configured EF Core behaviors, configure them directly on the `DbContext` before passing the queryable to `FlexQueryAsync`:

```csharp
var query = _context.Customers
    .IgnoreAutoIncludes()
    .IgnoreQueryFilters();

var result = await query.FlexQueryAsync(parameters, opts =>
{
    opts.UseNoTracking = true;
});
```

## ToSqlPreview

Inspect the SQL that EF Core will generate without executing the query:

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable().ApplyFilter(options);
string sql = query.ToQueryString(); // EF Core built-in method

// Returns the generated SQL or a diagnostic message
Console.WriteLine(sql);
```

**Note:** `ToQueryString()` is an EF Core built-in method. It is available after any step in the `IQueryable` pipeline.

## Real-World Example: Multi-Tenant API

```csharp
[HttpGet("customers")]
public async Task<IActionResult> GetCustomers(
    [FromQuery] FlexQueryParameters parameters)
{
    var tenantId = User.GetTenantId();

    var result = await _context.Customers
        .Where(p => p.CustomerId == tenantId)          // Customer isolation FIRST
        .FlexQueryAsync(parameters, opts =>
        {
            opts.AllowedFields = new HashSet<string>
            {
                "Id", "Name", "Email", "City", "Status", "Salary", "CreatedDate"
            };
            opts.BlockedFields = new HashSet<string> { "Email", "InternalNotes" };
            opts.StrictFieldValidation = true;
            opts.UseNoTracking = true;
            opts.MaxPageSize = 200;
        });

    return Ok(result);
}
```

## Performance Considerations

- **Always use `UseNoTracking`** for read-only endpoints — it significantly reduces memory allocation
- **Projection reduces data transfer** — `$select=Id,Name` prevents EF Core from loading unnecessary columns
- **COUNT queries are optional** — Set `IncludeCount = false` if you don't need total counts (saves a round-trip)
- **Filtered includes prevent over-fetching** — Loading `Orders(Status = 'Active')` is cheaper than loading all orders and filtering in memory
- **Expression caching** — FlexQuery caches compiled expression trees across identical query structures

## Common EF Core Limitations

| Limitation | Impact | Workaround |
|-----------|--------|------------|
| Complex projections with `GroupBy` may fail translation | EF Core cannot always translate arbitrary LINQ GroupBy expressions to SQL | Simplify the projection or use Dapper for complex aggregations |
| `Contains()` on large lists generates `IN (...)` with many parameters | SQL Server has a limit of ~2100 parameters | Batch large `IN` lists or use a temp table |
| Filtered includes don't support all predicates | EF Core restricts what can appear inside `Include().Where()` | Keep include filters simple (equality, comparisons) |
| `StringComparison` is not always translated | EF Core relies on database collation for case sensitivity | Configure database-level collation instead of `.ToLower()` |

## Security Considerations

- EF Core parameterizes all values automatically via FlexQuery's `ParameterWrapper<T>` — SQL injection is structurally impossible
- Always apply tenant isolation (`Where(x => x.TenantId == ...)`) **before** `FlexQueryAsync` to prevent cross-tenant data access
- Use `BlockedFields` to prevent clients from selecting or filtering on sensitive columns like `PasswordHash`, `InternalCost`, etc.

## Best Practices

1. **Apply tenant/security filters before FlexQueryAsync** — FlexQuery adds dynamic filters on top of your base query
2. **Use `UseNoTracking` for all query endpoints** — Only disable for update/delete endpoints
3. **Set `MaxPageSize`** — Prevent clients from requesting unbounded result sets
4. **Use projection** — Encourage clients to use `$select` to reduce data transfer
5. **Profile generated SQL** — Use EF Core logging or `ToSqlPreview()` to verify query efficiency

## Related Features

- [Dapper Provider](/providers/dapper/getting-started) — Alternative provider for raw SQL
- [AG Grid Adapter](/adapters/ag-grid) — Parsing AG Grid requests for EF Core
- [Security & Governance](/guide/security-governance) — Field-level access control
- [Field Mapping](/guide/field-mapping) — DTO-to-entity expression mapping
