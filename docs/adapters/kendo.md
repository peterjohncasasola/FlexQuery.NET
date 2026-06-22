# Kendo UI Integration

## Overview

`FlexQuery.NET.Adapters.Kendo` is a lightweight adapter that translates [Kendo UI DataSource](https://docs.telerik.com/kendo-ui/api/javascript/data/datasource) requests into FlexQuery's canonical `QueryOptions`. This allows you to power Kendo UI Grid's server-side operations — filtering, sorting, paging, grouping, and aggregation — with your existing FlexQuery backend.

### What It Is

Kendo UI DataSource sends a structured JSON payload when operating in server-side mode. This adapter parses that payload and converts it into `QueryOptions` that FlexQuery's execution pipeline already understands.

### Why It Exists

Kendo UI's request format is specific to Telerik's framework — it uses `take`/`skip` or `page`/`pageSize` for paging, a nested `filter` structure with `logic` and `filters` arrays, and `group`/`aggregate` for grouping and aggregation. This package eliminates the manual translation boilerplate.

### When to Use It

- Your frontend uses Kendo UI Grid with server-side DataSource operations
- You want server-side filtering, sorting, paging, grouping, and aggregation powered by FlexQuery
- You need nested filter logic (AND/OR groups within groups)

### When NOT to Use It

- You use Kendo UI Grid with client-side data binding only
- Your grid framework is not Kendo UI (use the core FlexQuery parameters instead)

## Installation

```bash
dotnet add package FlexQuery.NET.Adapters.Kendo
```

## Request Flow

```
Kendo UI Grid (Browser)
     │
     ▼ DataSource Request (JSON)
     │
ASP.NET Controller
     │
     ▼ KendoQueryOptionsParser.Parse()
         or
     request.ToQueryOptions()
         or
     json.FromKendoJson()
     │
QueryOptions (canonical AST)
     │
     ▼ FlexQueryAsync() — EF Core or Dapper
     │
QueryResult<T>
     │
     ▼ Return to Kendo UI Grid
```

## Basic Example

```csharp
[HttpPost("grid-data")]
public async Task<IActionResult> GetGridData([FromBody] JsonElement kendoPayload)
{
    var options = KendoQueryOptionsParser.Parse(kendoPayload);

    var result = await _context.Products.FlexQueryAsync<Product>(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
        opts.MaxPageSize = 200;
    });

    return Ok(result);
}
```

Using extension methods:

```csharp
// From a KendoRequest object
var options = kendoRequest.ToQueryOptions();

// From a raw JSON string
var options = jsonString.FromKendoJson();
```

## Advanced Example: Full Kendo Payload

```json
{
  "take": 20,
  "skip": 40,
  "filter": {
    "logic": "and",
    "filters": [
      { "field": "Status", "operator": "eq", "value": "Active" },
      {
        "logic": "or",
        "filters": [
          { "field": "Price", "operator": "gt", "value": 100 },
          { "field": "Priority", "operator": "eq", "value": "High" }
        ]
      }
    ]
  },
  "sort": [
    { "field": "Name", "dir": "asc" }
  ],
  "group": [
    {
      "field": "Category",
      "aggregates": [
        { "field": "Price", "aggregate": "sum" }
      ]
    }
  ],
  "aggregate": [
    { "field": "Price", "aggregate": "average" }
  ]
}
```

This single payload produces a `QueryOptions` with:
- **Paging**: Page 3, PageSize 20 (skip=40, take=20)
- **Filters**: (Status = 'Active' AND (Price > 100 OR Priority = 'High'))
- **Sorting**: Name ascending
- **GroupBy**: Category
- **Aggregates**: SUM(Price), AVG(Price)

## Extension Methods

In addition to `KendoQueryOptionsParser.Parse()`, the adapter provides three extension methods:

| Method | Description |
|--------|-------------|
| `KendoRequest.ToQueryOptions()` | Converts a `KendoRequest` object to `QueryOptions` |
| `string.FromKendoJson()` | Parses a JSON string containing a Kendo DataSource request |
| `QueryOptions.ApplyKendoRequest(KendoRequest)` | Merges a Kendo request into existing `QueryOptions` |

The `ApplyKendoRequest` extension is useful when you want to layer Kendo filters onto an already-configured query:

```csharp
var baseOptions = new QueryOptions { /* pre-configured options */ };
var result = baseOptions.ApplyKendoRequest(kendoRequest);
```

## Supported Features

### Pagination

Kendo UI supports two pagination modes, both handled automatically:

| Mode | Parameters | Example |
|------|-----------|---------|
| Take/Skip | `take` + `skip` | `take: 20, skip: 40` → Page 3, PageSize 20 |
| Page/PageSize | `page` + `pageSize` | `page: 2, pageSize: 50` → Page 2, PageSize 50 |
| Combined | all four | `take`/`skip` takes precedence if `take > 0` |

Pagination edge cases:
- `take: 0` and no `pageSize` → Pagination is skipped
- `skip: 0, take: 20` → Page 1, PageSize 20
- Both `take` and `page` provided → `take`/`skip` is preferred

### Filtering

Kendo UI uses a recursive filter structure. Each filter can be a simple condition or a nested logical group:

```
filter
├── logic: "and" | "or"
└── filters[]
    ├── { field, operator, value }            ← simple condition
    └── { logic, filters: [...] }             ← nested group
```

The adapter maps Kendo operators to FlexQuery operators:

| Kendo Operator | FlexQuery Operator |
|----------------|-------------------|
| `eq` | `eq` |
| `neq` | `neq` |
| `contains` | `contains` |
| `startswith` | `startswith` |
| `endswith` | `endswith` |
| `gt` | `gt` |
| `gte` | `gte` |
| `lt` | `lt` |
| `lte` | `lte` |
| `isnull` | `isnull` |
| `isnotnull` | `isnotnull` |
| `isempty` | `isnull` (aliased) |
| `isnotempty` | `isnotnull` (aliased) |

Nested filter groups are supported at any depth, mirroring Kendo's own recursive filter model.

### Sorting

The `sort` array maps directly to FlexQuery sort nodes:

```json
[
  { "field": "Name", "dir": "asc" },
  { "field": "CreatedAt", "dir": "desc" }
]
```

→ `Sort: [{ Field: "Name", Descending: false }, { Field: "CreatedAt", Descending: true }]`

### Grouping

The `group` array translates to `QueryOptions.GroupBy`:

```json
{ "group": [{ "field": "Category" }, { "field": "Region" }] }
```

→ `GroupBy: ["Category", "Region"]`

### Aggregations

Aggregates can be defined at two levels:

1. **Top-level** (`aggregate` array) — independent aggregates
2. **Group-level** (`group[].aggregates`) — aggregates scoped to grouped fields

The `aggregate` function name is normalized to FlexQuery's canonical form:

| Kendo aggregate | FlexQuery Function |
|-----------------|-------------------|
| `sum` | `sum` |
| `average` | `avg` |
| `avg` | `avg` |
| `min` | `min` |
| `max` | `max` |
| `count` | `count` |

```json
{
  "group": [{
    "field": "Category",
    "aggregates": [{ "field": "Price", "aggregate": "sum" }]
  }],
  "aggregate": [
    { "field": "Price", "aggregate": "average" }
  ]
}
```

→ `Aggregates: [{ Function: "sum", Field: "Price" }, { Function: "avg", Field: "Price" }]`

## Real-World Example: End-to-End with EF Core

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    [HttpPost("grid")]
    public async Task<IActionResult> GetOrders([FromBody] KendoRequest request)
    {
        // 1. Parse using extension method
        var options = request.ToQueryOptions();

        // 2. Execute with EF Core
        var result = await _db.Orders.FlexQueryAsync<Order>(options, opts =>
        {
            opts.AllowedFields = new HashSet<string>
            {
                "Id", "Customer", "Status", "Total", "OrderDate"
            };
        });

        // 3. Return in Kendo's expected format
        return Ok(new
        {
            Data = result.Data,
            Total = result.TotalCount
        });
    }
}
```

## Performance Considerations

- The adapter performs **zero database calls** — it only transforms JSON into `QueryOptions`
- JSON deserialization uses `System.Text.Json` for minimal allocation
- Nested filter groups are merged into flat structures where possible, optimizing the resulting expression tree
- For large grids, configure `MaxPageSize` to prevent unbounded result sets

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Kendo sends empty `filter: {}` (no `filters` array) | The adapter returns `null` — no filter applied |
| `aggregate: "average"` not recognized | It is! The adapter normalizes `"average"` to `"avg"` |
| `take` and `skip` both 0 | Falls back to `page`/`pageSize`; if also 0, pagination is skipped |
| Missing `field` in sort descriptor | Sort items without `field` are silently skipped |
| Deeply nested filter groups | Supported at any depth — the adapter recurses through all levels |

## Security Considerations

- **Always validate with `AllowedFields`** — Kendo UI sends whatever field names the grid defines. Configured `AllowedFields` prevents injection of unauthorized field names
- The adapter does not perform validation itself — it relies on FlexQuery's validation pipeline
- Treat the Kendo request body as **untrusted input**, just like any other API parameter

## Related Features

- [Query Syntax](/guide/query-syntax) — How FlexQuery's parser system works
- [Dapper Provider](/providers/dapper/getting-started) — Using parsed options with Dapper
- [EF Core Provider](/providers/ef-core) — Using parsed options with EF Core
- [Security & Governance](/guide/security-governance) — Field validation and access control
