# Basic Usage Guide

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
When paging is used, the result includes metadata:
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

Projection allows you to specify exactly which fields should be returned. This reduces database I/O and network bandwidth.

**Basic Select:**
`?select=Id,Name,Price`

**Nested Select:**
`?select=Id,Name,Category.Name`

> [!IMPORTANT]
> When using `select`, only the requested fields will be populated in the result object. All other fields will be null or default.

---

## Unified Execution

In FlexQuery v2, all these features are applied in a single unified pipeline. You don't need to call `.Where()`, `.OrderBy()`, or `.Select()` manually.

```csharp
var result = await _context.Users.FlexQueryAsync(parameters);
```

This single call handles:
1. Parsing the query string.
2. Building the Expression Tree.
3. Applying security rules.
4. Executing the query against the database.
