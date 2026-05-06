# Execution Pipeline

The execution pipeline is the heart of FlexQuery.NET. Understanding which method to call — and why — is critical for correctness, security, and performance.

---

## API Design & Positioning

FlexQuery.NET exposes `IQueryable` extension methods as the primary public API surface.

These extension methods provide:
- **Fluent composition**: Chain query steps naturally.
- **LINQ-style syntax**: Feels familiar to any .NET developer.
- **Cleaner code**: Reduces boilerplate in controllers.
- **Better readability**: Intent is clear at a glance.

The lower-level `QueryBuilder` APIs are considered **advanced/internal infrastructure** and are primarily intended for:
- Custom library integrations.
- Framework extensions (e.g., building a custom query provider).
- Complex execution scenarios where manual expression manipulation is required.


---

## Overview Table

| Method | Filter | Sort | Page | Project | Validate | Async | Returns |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| `ApplyFilter` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplySort` | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplyPaging` | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplySelect` | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | `IQueryable<object>` |
| `ApplyFilteredIncludes` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `FlexQuery` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | `QueryResult<object>` |
| `FlexQueryAsync` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `Task<QueryResult<object>>` |

---

## High-Level: FlexQueryAsync ⭐ Recommended

`FlexQueryAsync` is the **unified pipeline method**. It parses, validates, and executes in a single call.

**When to use:** Any standard public API endpoint.

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email", "status" };
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

**What it does internally:**

```
Parse(parameters)
  → ValidateOrThrow<T>(execOptions)
  → ApplyFilter
  → ApplySort
  → CountAsync (if IncludeCount = true)
  → ApplyPaging
  → ApplyFilteredIncludes
  → ApplySelect (if projection requested)
  → ToListAsync
  → QueryResult<object>
```

**Configuration:**

```csharp
await query.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields     = new HashSet<string> { "id", "name", "email" };
    exec.BlockedFields     = new HashSet<string> { "passwordHash" };
    exec.FilterableFields  = new HashSet<string> { "name", "status" };
    exec.SortableFields    = new HashSet<string> { "name", "createdAt" };
    exec.SelectableFields  = new HashSet<string> { "id", "name", "email" };
    exec.MaxFieldDepth     = 2;
    exec.StrictFieldValidation = true;
});
```

---

## Low-Level Methods

Use these when you need granular control over individual pipeline steps.

### ApplyFilter

Applies the `WHERE` predicate from `QueryOptions.Filter` to the query.

```csharp
var filtered = query.ApplyFilter(options);
```

- Returns `IQueryable<T>` — no database trip yet.
- No-op if `options.Filter` is null or empty.
- Builds an expression tree; EF Core translates it to SQL.

**Supported operators:** `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `contains`, `startswith`, `endswith`, `in`, `notin`, `between`, `isnull`, `isnotnull`, `like`, `any`, `all`, `count`

**Example:**

```
GET /api/users?filter=status:eq:active
```

```sql
-- Generated SQL
SELECT * FROM Users WHERE Status = 'active'
```

---

### ApplySort

Applies `ORDER BY` from `QueryOptions.Sort`.

```csharp
var sorted = query.ApplySort(options);
```

- Supports multiple sort fields (uses `ThenBy` internally).
- Supports aggregate sorts (e.g., sort by `Orders.count()`).
- No-op if `options.Sort` is empty.

**Example:**

```
GET /api/users?sort=name:asc,createdAt:desc
```

```sql
ORDER BY Name ASC, CreatedAt DESC
```

---

### ApplyPaging

Applies `SKIP` / `TAKE` from `QueryOptions.Paging`.

```csharp
var paged = query.ApplyPaging(options);
```

- Automatically adds a default `ORDER BY Id` if the query is unordered and `Skip > 0` (prevents EF Core errors).
- No-op if `options.Paging.Disabled = true`.

**Example:**

```
GET /api/users?page=2&pageSize=10
```

```sql
ORDER BY Id OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY
```

---

### ApplySelect

Applies dynamic projection. Returns `IQueryable<object>`.

```csharp
var projected = query.ApplySelect(options);
var data = await projected.ToListAsync();
```

- Uses expression trees — no reflection at runtime.
- Handles Nested, Flat, and FlatMixed modes.
- Delegates to `GroupByBuilder` when `GroupBy` or `Aggregates` are set.
- Returns `query.Cast<object>()` if no projection is requested.

**Example:**

```
GET /api/users?select=id,name,email
```

```json
[
  { "id": 1, "name": "Alice", "email": "alice@example.com" }
]
```

---

### ApplyFilteredIncludes

Applies the **Include pipeline** — EF Core `Include`/`ThenInclude` with optional inline filters.

```csharp
var withIncludes = query.ApplyFilteredIncludes(options);
```

- **Independent** from the WHERE pipeline — does not affect root result count.
- Must be called **before** `ToListAsync`.
- No-op if `options.FilteredIncludes` is null or empty.

**Example:**

```
GET /api/users?include=Orders(status:eq:shipped)
```

```csharp
// Translates to:
query.Include(u => u.Orders.Where(o => o.Status == "shipped"))
```


---

## Async Execution

All database trips should use the EF Core async extensions.

```csharp
// Count before paging
var total = await filteredQuery.CountAsync(cancellationToken);

// Execute after paging + projection
var data = await projectedQuery.ToListAsync(cancellationToken);
```

`FlexQueryAsync` handles all of this for you internally.

---

## ⚠️ Critical Warning: Double Filtering

> [!CAUTION]
> The most common mistake in FlexQuery.NET is applying filters twice.

**WRONG — This filters twice:**

```csharp
// ❌ DO NOT DO THIS
var options = QueryOptionsParser.Parse(parameters);

// Step 1: ApplyValidatedQueryOptions applies filter internally
var query = _context.Users.AsQueryable();
var query = query.ApplyValidatedQueryOptions(options);

// Step 2: ToProjectedQueryResultAsync ALSO applies filter internally
// The WHERE clause is duplicated in SQL!
var result = await query.ToProjectedQueryResultAsync(options);
```

**CORRECT — Use FlexQueryAsync:**

```csharp
// ✅ CORRECT: Everything in one call, filter applied once
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
});
```

**CORRECT — Manual pipeline, filter applied once:**

```csharp
// ✅ CORRECT: Manual pipeline — each step called exactly once
var options = QueryOptionsParser.Parse(parameters);
options.ValidateOrThrow<User>(execOptions);

var query = _context.Users.AsQueryable();
query = query.ApplyFilter(options);
query = query.ApplySort(options);

var total = await query.CountAsync();

query = query.ApplyPaging(options);
query = query.ApplyFilteredIncludes(options);

var data = await query.ApplySelect(options).ToListAsync();
return Ok(options.BuildQueryResult(data, total));
```

---

## Complete Manual Pipeline Example

For when you need full control — e.g., injecting custom tenant filter between steps:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    // 1. Parse
    var options = QueryOptionsParser.Parse(parameters);

    // 2. Validate
    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "id", "name", "email", "status", "createdAt" },
        MaxFieldDepth = 2
    };
    options.ValidateOrThrow<User>(execOptions);

    // 3. Start query
    var query = _context.Users
        .Where(u => u.TenantId == CurrentTenantId) // custom pre-filter
        .AsQueryable();

    // 4. Apply FlexQuery filter
    query = query.ApplyFilter(options);
    query = query.ApplySort(options);

    // 5. Count BEFORE paging
    var total = await query.CountAsync(ct);

    // 6. Page + includes
    query = query.ApplyPaging(options);
    query = query.ApplyFilteredIncludes(options);

    // 7. Project + execute
    var data = await query.ApplySelect(options).ToListAsync(ct);

    // 8. Return
    return Ok(options.BuildQueryResult(data, total));
}
```

---

## Deprecated Methods (v1 → v2)

The following methods are deprecated in v2 and will be removed in v3.

| Deprecated | Replacement |
| :--- | :--- |
| `ToQueryResultAsync` | `FlexQueryAsync` |
| `ToProjectedQueryResultAsync` | `FlexQueryAsync` |
| `ApplyValidatedQueryOptions` | Manual pipeline + `ValidateOrThrow<T>` |
| `QueryOptionsParser.Parse(QueryRequest)` | `QueryOptionsParser.Parse(FlexQueryParameters)` |

> [!WARNING]
> Deprecated methods are marked with `[Obsolete]` and hidden from IntelliSense. They will be removed in v3.
