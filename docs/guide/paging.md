# Paging

## Overview

FlexQuery.NET provides server-safe pagination with automatic defaults, configurable page size limits, and consistent response envelopes.

## Why this feature exists

Returning an unbounded result set from a public API is both a security risk and a performance trap. FlexQuery enforces server-side pagination contracts (default page size, maximum page size) while providing the client with rich pagination metadata so it can render paging controls accurately.

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
GET /api/customers?page=1&pageSize=20
GET /api/customers?page=2&pageSize=10
```

### Disable Total Count (faster)

```
GET /api/customers?page=1&pageSize=20&includeCount=false
```

---

## C# Examples

### Using ApplyPaging Directly

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable();
var paged = query.ApplyPaging(options);
var data = await paged.ToListAsync();
```

### Enforcing a Maximum Page Size

```csharp
var options = parameters.ToQueryOptions();

// Cap pageSize server-side before applying
if (options.Paging.PageSize > 100)
    options.Paging.PageSize = 100;

var query = _context.Customers.AsQueryable();
var paged = query.ApplyPaging(options);
```

### Getting Total Count

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable();
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
GET /api/customers?page=3&pageSize=5
```

**Response:**
```json
{
  "totalCount": 48,
  "resultCount": 48,
  "page": 3,
  "pageSize": 5,
  "totalPages": 10,
  "hasNextPage": true,
  "hasPreviousPage": true,
  "aggregates": null,
  "data": [
    { "id": 11, "name": "Lena Park" },
    { "id": 12, "name": "Mike Rowe" },
    { "id": 13, "name": "Nina Cole" },
    { "id": 14, "name": "Oscar Drew" },
    { "id": 15, "name": "Petra Voss" }
  ],
  "nextCursorToken": null
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
- Use **Keyset Pagination** (`?useKeysetPagination=true&cursor=TOKEN`) for very large datasets to avoid `OFFSET` scanning penalties.

---

## Keyset (Cursor) Pagination

For deep paging on large datasets, SQL `OFFSET` becomes exponentially slower because the database must scan and discard all preceding rows. Keyset Pagination (also known as cursor-based or seek pagination) solves this by using `WHERE` clauses (e.g., `WHERE Id > @cursor`) to resume exactly where the last page left off.

FlexQuery.NET natively supports keyset pagination.

### How to use Keyset Pagination

1. **Initial Request:** Set `useKeysetPagination=true` (or `options.UseKeysetPagination = true`) along with your `sort` criteria.

```http
GET /api/customers?sort=createdDate:desc,id:desc&pageSize=50&useKeysetPagination=true
```

2. **Retrieve the Token:** The response `QueryResult<T>` will include a `nextCursorToken` if there are more records.

```json
{
  "data": [ ... ],
  "nextCursorToken": "eyJ0eXAiOiJKV1QiLCJ..."
}
```

3. **Subsequent Requests:** Pass that token back in the `cursor` parameter to fetch the next page.

```http
GET /api/customers?sort=createdDate:desc,id:desc&pageSize=50&useKeysetPagination=true&cursor=eyJ0eXAiOiJKV1QiLCJ...
```

### Requirements for Keyset Pagination

- **Deterministic Sorting:** The query *must* have a unique sort order. If you sort by `createdAt`, you must include a unique tie-breaker like `id` (e.g. `sort=createdAt:desc,id:desc`), otherwise records sharing the same timestamp may be skipped or duplicated.
- **Unsupported Features:** Keyset pagination cannot be used simultaneously with `GROUP BY` or `DISTINCT` queries.
