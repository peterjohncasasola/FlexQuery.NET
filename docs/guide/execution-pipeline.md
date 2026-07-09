# Execution Pipeline

## Overview

The execution pipeline is the heart of FlexQuery.NET. It dictates the exact order of operations used to translate an incoming HTTP query into a database result. Understanding which pipeline method to call — and why — is critical for correctness, security, and performance.

## Why this feature exists

While `FlexQueryAsync` wraps the entire execution into a single, convenient call, enterprise applications often need to inject custom logic into the middle of the execution phase. For example, you might need to count the total rows in a multi-tenant system *after* applying the client's `WHERE` filter, but *before* you run secondary authorization checks on the data. The modular pipeline design exists so you can decouple the AST from execution.

## When to use

- Read this guide when you want to understand the difference between the `FlexQueryAsync` unified wrapper and the low-level `ApplyFilter` / `ApplySort` extension methods.
- Consult this guide if you are writing custom Database Providers (e.g., implementing an NHibernate or CosmosDB provider).

---

## API Design & Positioning

FlexQuery.NET exposes `IQueryable` extension methods as the primary public API surface for Entity Framework Core.

These extension methods provide:
- **Fluent composition**: Chain query steps naturally.
- **LINQ-style syntax**: Feels familiar to any .NET developer.
- **Cleaner code**: Reduces boilerplate in controllers.

---

## Overview Table

| Method | Filter | Sort | Page | Expand | Project | Validate | Returns |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| `ApplyFilter` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplySort` | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplyPaging` | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | `IQueryable<T>` |
| `ApplyExpand` | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | `IQueryable<T>` |
| `ApplySelect` | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | `IQueryable<object>` |
| `FlexQueryAsync` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `Task<QueryResult<T>>` |

---

## High-Level: FlexQueryAsync ⭐ Recommended

`FlexQueryAsync` is the **unified pipeline method**. It parses, validates, and executes in a single call.

**When to use:** Any standard public API endpoint.

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = ["Id", "Name", "Email", "Status"];
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

**What it does internally:**

```text
ToQueryOptions(parameters)
  → ValidateOrThrow(execOptions)
  → ApplyFilter
  → ApplySort
  → CountAsync (if IncludeCount = true and not keyset)
  → ApplyPaging
  → ApplyExpand (previously FilteredIncludes)
  → ApplySelect (if projection requested)
  → ToListAsync
  → QueryResult<T>
```

---

## Low-Level Methods

Use these when you need granular control over individual pipeline steps.

### ApplyFilter

Applies the `WHERE` predicate from `QueryOptions.Filter` to the `IQueryable`.

```csharp
var filtered = query.ApplyFilter(options);
```

- Returns `IQueryable<T>` — no database trip yet.
- No-op if `options.Filter` is null or empty.
- Builds an expression tree; EF Core translates it to SQL.

**Example:**
`GET /api/users?filter=Status:eq:active`
```sql
SELECT * FROM Users WHERE Status = 'active'
```

---

### ApplySort

Applies `ORDER BY` from `QueryOptions.Sort`.

```csharp
var sorted = query.ApplySort(options);
```

- Supports multiple sort fields (uses `ThenBy` internally).
- No-op if `options.Sort` is empty.

**Example:**
`GET /api/users?sort=Name:asc,CreatedAt:desc`
```sql
ORDER BY Name ASC, CreatedAt DESC
```

---

### ApplyPaging

Applies `SKIP` / `TAKE` (Offset pagination) or a `WHERE Cursor > X` (Keyset pagination) from `QueryOptions.Paging`.

```csharp
var paged = query.ApplyPaging(options);
```

- Automatically adds a default `ORDER BY Id` if the query is unordered and `Skip > 0` (prevents EF Core errors in SQL Server).

---

### ApplyExpand (Formerly Includes)

Applies the **Include pipeline** — EF Core `Include`/`ThenInclude` with optional inline filters.

```csharp
var withIncludes = query.ApplyExpand(options);
```

- Must be called **before** `ToListAsync`.
- No-op if `options.Expand` is null or empty.

**Example:**
`GET /api/users?include=Orders(Status:eq:shipped)`
```csharp
// Translates internally to:
query.Include(u => u.Orders.Where(o => o.Status == "shipped"))
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
- Returns `query.Cast<object>()` if no projection is requested.

---

## ⚠️ Critical Warning: Double Filtering

> [!CAUTION]
> The most common mistake in FlexQuery.NET manual pipeline orchestration is applying filters twice.

**WRONG — This filters twice:**

```csharp
// ❌ DO NOT DO THIS
var options = parameters.ToQueryOptions();

var query = _context.Users.AsQueryable();

// 1st Filter: Applied here manually
query = query.ApplyFilter(options);

// 2nd Filter: FlexQueryAsync re-applies the options!
// The WHERE clause is duplicated in SQL!
var result = await query.FlexQueryAsync(options); 
```

**CORRECT — Use the Unified Pipeline:**

```csharp
// ✅ CORRECT: Everything in one call, filter applied once
var result = await _context.Users.FlexQueryAsync(parameters, exec =>
{
    exec.AllowedFields = ["Id", "Name", "Email"];
});
```

---

## Complete Manual Pipeline Example

For when you need full control — e.g., injecting custom tenant filter between steps and logging the SQL:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsersManual([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    // 1. Parse
    var options = parameters.ToQueryOptions();

    // 2. Validate against Server Policy
    var execOptions = new EfCoreQueryOptions
    {
        AllowedFields = ["Id", "Name", "Email", "Status", "CreatedAt"],
        MaxFieldDepth = 2
    };
    options.ValidateOrThrow(execOptions);

    // 3. Start query with strict Tenancy limits
    var query = _context.Users
        .Where(u => u.TenantId == CurrentTenantId) 
        .AsQueryable();

    // 4. Apply FlexQuery filter and sort
    query = query.ApplyFilter(options);
    query = query.ApplySort(options);

    // 5. Manual intervention: Count BEFORE paging
    var total = await query.CountAsync(ct);

    // 6. Page + Includes
    query = query.ApplyPaging(options);
    query = query.ApplyExpand(options);

    // 7. Project + Execute
    var data = await query.ApplySelect(options).ToListAsync(ct);

    // 8. Return standardized envelope
    return Ok(options.BuildQueryResult(data, total));
}
```

---

## Deprecated Methods

The following methods were heavily used in v1/v2 but are removed or completely deprecated in v4:

| Deprecated Method | Replacement |
| :--- | :--- |
| `ToQueryResultAsync` | `FlexQueryAsync` |
| `ToProjectedQueryResultAsync` | `FlexQueryAsync` |
| `ApplyValidatedQueryOptions` | `FlexQueryAsync` or Manual pipeline |
| `QueryOptionsParser.Parse` | `parameters.ToQueryOptions()` |
| `ApplyFilteredIncludes` | `ApplyExpand` |

> [!WARNING]
> Deprecated methods have been formally removed from the `v4.0.0` distribution to streamline the API.
