# Sorting

## Overview

FlexQuery.NET supports multi-field sorting with simple syntax, including aggregate-based sorting on related collections.

## Why this feature exists

Frontend data grids require dynamic sort control — clicking a column header should immediately reorder results. Without a library like FlexQuery, each sortable column requires explicit `switch/case` handling on the backend. FlexQuery translates any valid sort expression into an optimized, multi-level `ORDER BY` chain.

---

## What It Does

Sorting applies `ORDER BY` clauses to your `IQueryable`. It supports:

- Multiple sort fields with independent directions
- Ascending and descending per field
- Aggregate sorting on collection navigation properties (`count`, `sum`, `avg`, `min`, `max`)
- Composed `ThenBy` chains automatically

---

## When to Use

Use sorting any time the client should control the order of results — lists, tables, dashboards, reports.

---

## When NOT to Use

- Do not allow sorting on unbounded text columns without `SortableFields` restrictions.
- Do not sort on collection navigation properties directly — use aggregate sort syntax instead.

---

## HTTP Examples

### Single Field

```
GET /api/customers?sort=name:asc
GET /api/customers?sort=createdDate:desc
```

### Multiple Fields

Fields are applied in order (first is primary, subsequent are `ThenBy`):

```
GET /api/customers?sort=status:asc,name:asc,createdDate:desc
```

SQL:
```sql
ORDER BY Status ASC, Name ASC, CreatedAt DESC
```

### Default Direction

Omitting the direction defaults to ascending:

```
GET /api/customers?sort=name
```

### Aggregate Sort

Sort by a computed aggregate over a related collection:

```
GET /api/customers?sort=orders.count():desc
GET /api/customers?sort=orders.sum(total):desc
GET /api/customers?sort=orders.avg(total):asc
GET /api/customers?sort=orders.max(total):desc
GET /api/customers?sort=orders.min(total):asc
```

---

## C# Examples

### Using QueryBuilder.ApplySort Directly

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable();
var sorted = query.ApplySort(options);
var data = await sorted.ToListAsync();
```

### Sort with Validation

```csharp
var execOptions = new QueryExecutionOptions
{
    SortableFields = new HashSet<string> { "name", "createdAt", "age" }
};
options.ValidateOrThrow<Customer>(execOptions);

var query = _context.Customers.AsQueryable();
var sorted = query.ApplySort(options);
```

### Programmatic Sort (Fluent API)

```csharp
// Build sort options in code without parsing a query string
var options = new QueryOptions
{
    Sort = new List<SortNode>
    {
        new SortNode { Field = "name",      Descending = false },
        new SortNode { Field = "createdAt", Descending = true  }
    }
};

var query = _context.Customers.AsQueryable();
var sorted = query.ApplySort(options);
```

---

## JSON Output Example

**Request:**
```
GET /api/customers?sort=createdDate:desc&page=1&pageSize=3
```

**Response:**
```json
{
  "totalCount": 48,
  "resultCount": 48,
  "page": 1,
  "pageSize": 3,
  "totalPages": 16,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 10, "name": "Zara Khan",   "createdDate": "2025-11-20T09:00:00Z" },
    { "id": 9,  "name": "Yuki Tanaka", "createdDate": "2025-10-15T14:30:00Z" },
    { "id": 8,  "name": "Xan Torres",  "createdDate": "2025-09-01T08:00:00Z" }
  ],
  "nextCursorToken": null
}
```

---

## Common Mistakes

### ❌ Sorting on collection navigation directly

```
# WRONG — cannot sort by a collection property
GET /api/customers?sort=orders:desc
```

Use aggregate sort instead:

```
# CORRECT
GET /api/customers?sort=orders.count():desc
```

### ❌ Unrestricted sort fields on public API

```csharp
// WRONG — client could sort by passwordHash or internal fields
var result = await _context.Customers.FlexQueryAsync(parameters);
```

```csharp
// CORRECT
var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
{
    exec.SortableFields = new HashSet<string> { "name", "email", "createdAt" };
});
```

---

## Performance Notes

- Multi-field sorts use `ThenBy`/`ThenByDescending` — EF Core generates a single efficient `ORDER BY`.
- Aggregate sorts (e.g., `orders.count()`) use `Enumerable.Count()` for in-memory collection navigation. Ensure the collection is loaded or use `AsQueryable()` for server-side aggregation.
- Avoid sorting on unindexed text columns in large tables — ensure your sorted columns are indexed.
- Always sort before paging. FlexQuery.NET enforces this order automatically.
