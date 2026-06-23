# Paging

FlexQuery.NET provides server-safe pagination with automatic defaults and consistent response envelopes.

---

## What It Does

Paging applies `SKIP` and `TAKE` to your `IQueryable`. It:

- Converts page number + page size to OFFSET/FETCH
- Automatically adds a default `ORDER BY` if needed to prevent EF Core errors
- Returns pagination metadata in the `QueryResult` envelope

---

## Counting Semantics

`QueryResult<T>` exposes three related counts:

| Property | Meaning |
| ----------- | ------------------------- |
| `TotalCount` | Filtered source records |
| `ResultCount` | Shaped rows before paging |
| `Data.Count` | Current page rows |

`TotalCount` preserves the existing FlexQuery behavior: it counts source records after filtering and before paging. `ResultCount` counts the rows produced by the final query shape before paging. For normal queries they usually match.

```text
1432 products
pageSize = 20

TotalCount  = 1432
ResultCount = 1432
Data.Count  = 20
```

For grouped, distinct, pivoted, or otherwise shaped queries, `ResultCount` can be smaller than `TotalCount`:

```text
1432 products
GROUP BY Brand

4 brand groups

TotalCount  = 1432
ResultCount = 4
Data.Count  = current page of groups
```

For `HAVING`, `ResultCount` is calculated after the grouped rows are filtered:

```text
1432 products
GROUP BY Brand
HAVING SUM(Quantity) > 100

2 groups remain

TotalCount  = 1432
ResultCount = 2
```

Adapters and UI grids that page grouped results should generally use:

```csharp
var rowCount = result.ResultCount ?? result.TotalCount;
```

---

## When to Use

Use paging on **any list endpoint** to prevent full-table scans and large network payloads.

---

## When NOT to Use

- Do not disable paging on endpoints that return large datasets without a plan for rate limiting.
- Do not rely on client-provided `pageSize` without a server-side maximum.

---

## HTTP Examples

### Basic Paging

```
GET /api/users?page=1&pageSize=20
GET /api/users?page=2&pageSize=10
```

### Disable Total Count (faster)

```
GET /api/users?page=1&pageSize=20&includeCount=false
```

---

## C# Examples

### Using ApplyPaging Directly

```csharp
var options = QueryOptionsParser.Parse(parameters);
var query = _context.Users.AsQueryable();
var paged = query.ApplyPaging(options);
var data = await paged.ToListAsync();
```

### Enforcing a Maximum Page Size

```csharp
var options = QueryOptionsParser.Parse(parameters);

// Cap pageSize server-side before applying
if (options.Paging.PageSize > 100)
    options.Paging.PageSize = 100;

var query = _context.Users.AsQueryable();
var paged = query.ApplyPaging(options);
```

### Getting Total Count

```csharp
var options = QueryOptionsParser.Parse(parameters);
var query = _context.Users.AsQueryable();
var filtered = query.ApplyFilter(options);
var filtered2 = filtered.ApplySort(options);

// Count filtered source records BEFORE paging.
var total = await filtered2.CountAsync();

var paged = filtered2.ApplyPaging(options);
var data = await paged.ToListAsync();

return Ok(options.BuildQueryResult(data, total));
```

---

## PagingOptions Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Page` | `int` | `1` | Current page number (1-indexed) |
| `PageSize` | `int` | `20` | Number of items per page |
| `Skip` | `int` | computed | Computed as `(Page - 1) * PageSize` |
| `Disabled` | `bool` | `false` | If true, no SKIP/TAKE is applied |

---

## JSON Output Example

**Request:**
```
GET /api/users?page=3&pageSize=5
```

**Response:**
```json
{
  "data": [
    { "id": 11, "name": "Lena Park" },
    { "id": 12, "name": "Mike Rowe" },
    { "id": 13, "name": "Nina Cole" },
    { "id": 14, "name": "Oscar Drew" },
    { "id": 15, "name": "Petra Voss" }
  ],
  "totalCount": 48,
  "resultCount": 48,
  "page": 3,
  "pageSize": 5
}
```

---

## Common Mistakes

### ❌ Not counting before paging

```csharp
// WRONG — count includes paging offset, returns wrong number
var paged = query.ApplyPaging(options);
var total = await paged.CountAsync(); // WRONG
```

```csharp
// CORRECT — count BEFORE paging
var filtered = query.ApplyFilter(options);
var total = await filtered.CountAsync(); // CORRECT
var paged = filtered.ApplyPaging(options);
```

### ❌ No server-side cap on pageSize

Allow clients to request `pageSize=10000` without a limit — this will cause full-table fetches.

```csharp
// CORRECT — cap pageSize
if (options.Paging.PageSize > 200) options.Paging.PageSize = 200;
```

---

## Performance Notes

- `CountAsync` runs a separate SQL `COUNT(*)` query. On large tables, this can be slow.
- Disable `IncludeCount` (`?includeCount=false`) on high-frequency endpoints where total count is not needed.
- Grouped or shaped queries may also calculate `ResultCount` from the shaped query before paging.
- Always sort before paging. Without a deterministic `ORDER BY`, results are undefined.
- Use cursor-based pagination for very large datasets — FlexQuery.NET handles standard offset paging.
