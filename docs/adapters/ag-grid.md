# AG Grid Integration

## Overview

`FlexQuery.NET.Adapters.AgGrid` is a lightweight adapter that translates [AG Grid Enterprise](https://www.ag-grid.com/) Server-Side Row Model requests into FlexQuery's canonical `QueryOptions`. This allows you to power AG Grid's advanced features — pagination, sorting, filtering, row grouping, and aggregation — with your existing FlexQuery backend, whether you're using EF Core or Dapper.

### What It Is

AG Grid Enterprise sends a specific JSON payload (`IServerSideGetRowsRequest`) when using the Server-Side Row Model. This adapter parses that payload and converts it into `QueryOptions` that FlexQuery's execution pipeline already understands. No custom SQL, no manual mapping.

### Why It Exists

AG Grid's request format is specific to AG Grid — it uses `startRow`/`endRow` instead of `page`/`pageSize`, `filterModel` instead of a filter string, and `rowGroupCols`/`valueCols` for aggregation. Without an adapter, you would need to manually translate each of these structures. This package eliminates that boilerplate.

### When to Use It

- Your frontend uses AG Grid Enterprise with the Server-Side Row Model
- You want server-side pagination, filtering, and sorting powered by FlexQuery
- You need row grouping and aggregation computed on the server

### When NOT to Use It

- You use AG Grid's Client-Side Row Model (all data is loaded upfront — no server requests)
- You use AG Grid Community Edition without server-side features
- Your grid framework is not AG Grid (use the core FlexQuery parameters instead)

## Installation

```bash
dotnet add package FlexQuery.NET.Adapters.AgGrid
```

## Request Flow

```
AG Grid Enterprise (Browser)
     │
     ▼ IServerSideGetRowsRequest (JSON)
     │
ASP.NET Controller
     │
     ▼ AgGridQueryOptionsParser.Parse()
     │
QueryOptions (canonical AST)
     │
     ▼ FlexQueryAsync() — EF Core or Dapper
     │
QueryResult<T>
     │
     ▼ Return to AG Grid
```

## Basic Example

```csharp
[HttpPost("grid-data")]
public async Task<IActionResult> GetGridData([FromBody] JsonElement agGridPayload)
{
    var options = AgGridQueryOptionsParser.Parse(agGridPayload);

    var result = await _context.Products.FlexQueryAsync<Product>(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
        opts.MaxPageSize = 200;
    });

    return Ok(result);
}
```

## Advanced Example: Full AG Grid Payload

```json
{
  "startRow": 0,
  "endRow": 100,
  "filterModel": {
    "Status": { "filterType": "text", "type": "equals", "filter": "Active" },
    "Price": { "filterType": "number", "type": "greaterThan", "filter": 50 }
  },
  "sortModel": [
    { "colId": "Name", "sort": "asc" }
  ],
  "rowGroupCols": [
    { "field": "Category" }
  ],
  "valueCols": [
    { "field": "Price", "aggFunc": "sum" },
    { "field": "Price", "aggFunc": "average" }
  ]
}
```

This single payload produces a `QueryOptions` with:
- **Paging**: Page 1, PageSize 100
- **Filters**: Status = 'Active' AND Price > 50
- **Sorting**: Name ascending
- **GroupBy**: Category
- **Aggregates**: SUM(Price), AVG(Price)

## Supported Features

### Pagination

AG Grid uses `startRow`/`endRow` (zero-based indices). The adapter converts these to FlexQuery's `Page`/`PageSize`:

```
startRow: 0, endRow: 100  →  Page: 1, PageSize: 100
startRow: 100, endRow: 200  →  Page: 2, PageSize: 100
startRow: 50, endRow: 75  →  Page: 3, PageSize: 25
```

Edge cases are handled gracefully:
- `endRow <= startRow` → Pagination is skipped
- `startRow < 0` → Treated as 0
- Division by zero (when `pageSize` would be 0) → Pagination disabled

### Filtering

The adapter translates AG Grid's `filterModel` structure. Each column can have its own filter type:

| AG Grid Filter Type | FlexQuery Operator |
|---------------------|-------------------|
| `equals` | `eq` |
| `notEqual` | `neq` |
| `contains` | `contains` |
| `notContains` | `notcontains` |
| `startsWith` | `startswith` |
| `endsWith` | `endswith` |
| `lessThan` | `lt` |
| `lessThanOrEqual` | `lte` |
| `greaterThan` | `gt` |
| `greaterThanOrEqual` | `gte` |
| `inRange` | `between` |

Compound conditions (AND/OR) within a single column are supported via the `conditions` array with an `operator` field.

### Sorting

The `sortModel` array maps directly to FlexQuery sort nodes:

```json
[
  { "colId": "Name", "sort": "asc" },
  { "colId": "CreatedAt", "sort": "desc" }
]
```

### Row Grouping

`rowGroupCols` translates to `QueryOptions.GroupBy`:

```json
{ "rowGroupCols": [{ "field": "Category" }, { "field": "Region" }] }
```

→ `GroupBy: ["Category", "Region"]`

### Aggregations

`valueCols` translates to `QueryOptions.Aggregates`. The `aggFunc` is normalized to FlexQuery's canonical function names:

| AG Grid aggFunc | FlexQuery Function |
|----------------|-------------------|
| `sum` | `sum` |
| `min` | `min` |
| `max` | `max` |
| `avg` | `avg` |
| `average` | `avg` (normalized) |
| `count` | `count` |

```json
{ "valueCols": [{ "field": "Price", "aggFunc": "sum" }] }
```

→ `Aggregates: [{ Function: "sum", Field: "Price", Alias: "priceSum" }]`

## SSRM Response Conversion

The adapter provides `ToAgGridServerSideResponse()` to convert a FlexQuery `QueryResult` into an AG Grid Server-Side Row Model response:

```csharp
[HttpPost("grid")]
public async Task<IActionResult> GetGridData([FromBody] AgGridRequest request)
{
    var options = request.ToQueryOptions();
    var result = await _context.Products.FlexQueryAsync<Product>(options);
    var response = result.ToAgGridServerSideResponse(request);
    return Ok(response);
}
```

The converter handles two response types:

| Request Level | Response Type | Description |
|---|---|---|
| **Grouped** (`rowGroupCols.length > groupKeys.length`) | Group rows with metadata | Adds `group`, `key`, `field`, `level`, `leafGroup`, `groupKeys`, `childCount` fields |
| **Leaf** (`rowGroupCols.length == groupKeys.length` or no grouping) | Passthrough rows | Returns original response data unchanged |

### Group Row Metadata

Group rows include adapter-defined metadata fields for AG Grid SSRM callbacks:

| Response Field | Callback | Description |
|---|---|---|
| `group` | `isServerSideGroup` | Always `true` for group rows |
| `key` | `getServerSideGroupKey` | The group key value (e.g., `"Electronics"`) |
| `field` | — | The field name of the grouped column |
| `level` | — | Zero-based grouping depth |
| `leafGroup` | — | `true` if expanding this group returns leaf rows |
| `groupKeys` | `getServerSideGroupKey` | Full route including all ancestor keys |
| `childCount` | `getChildCount` | Number of child rows within this group |

### Aggregate Alias Mapping

When AG Grid groups rows, it expects aggregate values under the **original column field name** — not under FlexQuery's aggregate alias. For example, a column with `field: "quantity"` and `aggFunc: "SUM"` expects `data.quantity`.

The adapter automatically maps FlexQuery aggregate aliases back to the original field name in the response:

| Scenario | FlexQuery Result | AG Grid Response | Behavior |
|---|---|---|---|
| Single aggregate per field | `quantitySum: 494220` | `quantity: 494220` | Mapped transparently |
| Multiple aggregates, same field | `quantitySum: 494220, quantityAvg: 120` | `quantitySum: 494220, quantityAvg: 120` | Aliases preserved (no overwrite) |

**Rule**: If a field has exactly one aggregate in the request's `valueCols`, the alias is renamed to the field name. If a field has multiple aggregates (e.g., both `sum` and `avg` on `quantity`), all aliases are kept unchanged to avoid collisions.

This means:
- **Single-aggregate columns work out-of-the-box** — no `valueGetter` or column definition changes needed
- **Multi-aggregate columns** require the frontend to reference the alias (e.g., `valueGetter: p => p.data.quantityAvg`) or use `field: "quantityAvg"` in the column definition

The field names for `group`, `key`, `field`, `level`, `leafGroup`, `groupKeys`, and `childCount` are configurable via `AgGridResponseFieldOptions`:

```csharp
var options = new AgGridResponseFieldOptions
{
    GroupFlagFieldName = "__group",
    KeyFieldName = "__key",
    FieldFieldName = "__field",
    LevelFieldName = "__level",
    LeafGroupFieldName = "__leaf",
    RouteFieldName = "__route",
    ChildCountFieldName = "__count",
    ChildCountSourceField = "childCount"
};

var response = result.ToAgGridServerSideResponse(request, options);
```

## Real-World Example: End-to-End with Dapper

```csharp
[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IDbConnection _db;

    public SalesController(IDbConnection db) => _db = db;

    [HttpPost("grid")]
    public async Task<IActionResult> GetSalesData([FromBody] AgGridRequest request)
    {
        // 1. Parse the AG Grid payload
        var options = request.ToQueryOptions();

        // 2. Execute with Dapper
        var result = await ((DbConnection)_db).FlexQueryAsync<SalesRecord>(options, opts =>
        {
            opts.Dialect = new SqlServerDialect();
            opts.AllowedFields = new HashSet<string>
            {
                "Id", "Product", "Category", "Region", "Amount", "SaleDate"
            };
        });

        // 3. Return in AG Grid's expected format
        return Ok(new
        {
            rowData = result.Data,
            rowCount = result.TotalCount
        });
    }
}
```

## Performance Considerations

- The adapter performs **zero database calls** — it only transforms JSON into `QueryOptions`
- JSON deserialization uses `System.Text.Json` for minimal allocation
- Aggregation queries are executed as separate SQL statements only when `valueCols` is present without `rowGroupCols`
- For very large grids, configure `MaxPageSize` to prevent clients from requesting unbounded result sets

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| AG Grid sends empty `filterModel: {}` | The adapter handles this gracefully — no filters applied |
| `aggFunc: "average"` not recognized | It is! The adapter normalizes `"average"` to `"avg"` automatically |
| `startRow` and `endRow` are both 0 | Pagination is skipped when `endRow <= startRow` |
| Missing `colId` in sortModel | Sort items without `colId` are silently skipped |
| Using with AG Grid Community | Community edition doesn't send server-side requests — this adapter is for Enterprise only |

## Security Considerations

- **Always validate with `AllowedFields`** — AG Grid sends whatever column IDs the frontend defines. If a user modifies the grid configuration, they could send arbitrary field names
- The adapter does not perform validation itself — it relies on FlexQuery's validation pipeline to reject unauthorized fields
- Treat the AG Grid request body as **untrusted input**, just like any other API parameter

## Related Features

- [Query Syntax](/guide/query-syntax) — How FlexQuery's parser system works
- [Dapper Provider](/providers/dapper/getting-started) — Using parsed options with Dapper
- [EF Core Provider](/providers/ef-core) — Using parsed options with EF Core
- [Security & Governance](/guide/security-governance) — Field validation and access control
