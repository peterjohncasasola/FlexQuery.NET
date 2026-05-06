> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Basic Usage Guide (v1.x Legacy)

FlexQuery.NET uses a consistent, human-readable DSL (Domain Specific Language) for dynamic querying. The standard format for any operation is `Field:Operator:Value`.

## Filtering

Filtering allows you to restrict the results based on property values.

### Simple Filters
- **Equals**: `Name:eq:John`
- **Contains**: `Name:contains:John`
- **Greater Than**: `Price:gt:100`
- **In Collection**: `Status:in:Active,Pending`

### Multiple Filters
By default, multiple filters are combined using **AND**.
`?filter=Status:eq:Active,Price:gt:100`

### Nested Properties
You can filter on nested navigation properties using dot notation.
`?filter=Category.Name:eq:Electronics`

---

## Sorting

Sorting controls the order of the returned items.

- **Ascending**: `?sort=Name:asc`
- **Descending**: `?sort=Price:desc`
- **Multiple**: `?sort=Category.Name:asc,Price:desc`

---

## Paging

FlexQuery supports standard paging parameters.

- **Page**: The current page number (1-based). `?page=1`
- **PageSize**: Number of items per page. `?pageSize=20`

**Result Shape:**
When paging is used via `ToQueryResultAsync`, the result includes metadata:
```json
{
  "items": [...],
  "totalCount": 150,
  "totalPages": 8,
  "currentPage": 1,
  "pageSize": 20
}
```

---

## Projection (Select)

Projection allows you to specify exactly which fields should be returned.

**Basic Select:**
`?select=Id,Name,Price`

**Nested Select:**
`?select=Id,Name,Category.Name`

---

## Execution in v1.x

In v1.x, you typically use `ToQueryResultAsync` or `ToProjectedQueryResultAsync` for execution.

```csharp
// Returns full entities
var result = await _context.Users.ToQueryResultAsync(options);

// Returns projected objects (respects ?select=...)
var result = await _context.Users.ToProjectedQueryResultAsync(options);
```

These extension methods handle:
1. Building the Expression Tree.
2. Applying the logic to the `IQueryable`.
3. Executing the query against the database.
