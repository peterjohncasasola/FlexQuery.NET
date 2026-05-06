> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Execution Methods

Choosing the correct execution method is critical to ensure both the security and performance of your API. This guide explains the different ways to apply queries and highlights common pitfalls to avoid.

## Overview Table

| Method | Applies Filter | Projection | Validation | Async | Returns |
| :--- | :---: | :---: | :---: | :---: | :--- |
| `ApplyQueryOptions` | ✅ | ❌ | ❌ | ➖ | IQueryable\<T\> |
| `ApplyValidatedQueryOptions` | ✅ | ❌ | ✅ | ➖ | IQueryable\<T\> |
| `ApplySelect` | ❌ | ✅ | ❌ | ➖ | IQueryable\<object\> |
| `ApplyFilteredIncludes` | ❌ | ❌ | ❌ | ➖ | IQueryable\<T\> |
| `ToQueryResult` | ✅ | ❌ | ✅ | ❌ | QueryResult\<T\> |
| `ToQueryResultAsync` | ✅ | ❌ | ✅ | ✅ | Task\<QueryResult\<T\>\> |
| `ToProjectedQueryResult` | ✅ | ✅ | ❌ | ❌ | QueryResult\<object\> |
| `ToProjectedQueryResultAsync` | ✅ | ✅ | ❌ | ✅ | Task\<QueryResult\<object\>\> |

---

## ⚠️ Validation vs. Execution

It is vital to understand where validation happens to prevent performance issues or runtime errors.

- **ApplyValidatedQueryOptions**: This method **VALIDATES** the options against your model and **APPLIES** the filtering/sorting logic to the `IQueryable`.
- **ToProjectedQueryResultAsync**: This method **APPLIES** filtering, **APPLIES** projection/includes, and **EXECUTES** the query against the database. 

> [!CAUTION]
> **Important**: `ToProjectedQueryResultAsync` does **not** automatically run validation. You must validate your options manually before calling it if you are exposing the endpoint to the public.

---

## ❌ Common Mistake: Double Filtering

A frequent error is applying query options twice. This results in the filtering logic being injected into the SQL query twice, which is redundant and can cause unexpected behavior.

```csharp
// INCORRECT USAGE
var options = QueryOptionsParser.Parse(request);

// 1. First application + validation
var query = _context.Users.AsQueryable();
var query = query.ApplyValidatedQueryOptions(options);

// 2. Second application (Internal call inside result wrapper)
// ERROR: This applies the SAME filters again!
var result = await query.ToProjectedQueryResultAsync(options);
```

---

## ✅ Correct Pattern: Manual Validation

When you need both validation and the full projection pipeline, you should use the `QueryValidator` separately.

```csharp
// CORRECT USAGE
var options = QueryOptionsParser.Parse(request);

// 1. Validate ONLY
var validator = new QueryValidator();
var validation = validator.Validate<User>(options);

if (!validation.IsValid)
{
    return BadRequest(validation.Errors);
}

// 2. Execute full pipeline (Filtering + Projection + Execution)
var result = await _context.Users.ToProjectedQueryResultAsync(options);
```

---

## Method Breakdown

### Pipeline Methods
These methods return an `IQueryable`, allowing for further LINQ chaining.

- **ApplyQueryOptions**: Applies filters, sorts, and paging. No validation is performed.
- **ApplyValidatedQueryOptions**: Validates input and applies filters. Throws `QueryValidationException` if validation fails.
- **ApplySelect**: Applies the projection tree and handles field aliases.
- **ApplyFilteredIncludes**: Applies filters to related collections (Filtered Includes pipeline).

### Result Methods (Sync & Async)
These methods execute the query and return a `QueryResult` object containing the data and paging metadata.

- **ToQueryResult / ToQueryResultAsync**: Executes the query and returns full entity objects.
- **ToProjectedQueryResult / ToProjectedQueryResultAsync**: Executes the full pipeline, including projection and shaped includes. This is the recommended approach for modern, flexible APIs.

---

# Low-Level Pipeline Methods

These methods provide fine-grained control over query execution and can be composed manually to create custom processing flows.

## Method Breakdown

### ApplyFilter
Applies the `WHERE` conditions from the `QueryOptions`. Supports nested AND/OR groups and collection predicates like `any` and `all`.
```csharp
query.ApplyFilter(options);
```

### ApplySort
Applies the `ORDER BY` logic. Supports multiple fields and directions.
```csharp
query.ApplySort(options);
```

### ApplyPaging
Applies the `SKIP` and `TAKE` (or `OFFSET` and `FETCH`) logic for pagination.
```csharp
query.ApplyPaging(options);
```

### ApplySelect
Applies the projection pipeline, transforming the result shape and applying aliases.
```csharp
var projected = query.ApplySelect(options);
```

### ApplyFilteredIncludes
Applies filters to related collections. This is part of the dual-pipeline system and works independently from the root `WHERE` filter.
```csharp
query.ApplyFilteredIncludes(options);
```

---

## Manual Pipeline Examples

### 1. Basic Manual Pipeline
```csharp
var query = _context.Users.AsQueryable();

query = query.ApplyFilter(options);
query = query.ApplySort(options);
query = query.ApplyPaging(options);

var result = await query.ToListAsync();
```

### 2. Projection Pipeline
```csharp
var query = _context.Users.AsQueryable();

query = query.ApplyFilter(options);
query = query.ApplySort(options);

var projected = query.ApplySelect(options);

var result = await projected.ToListAsync();
```

### 3. Full Control (Advanced)
```csharp
var query = _context.Users.AsQueryable();

query = query.ApplyFilter(options);
query = query.ApplySort(options);
query = query.ApplyPaging(options);

query = query.ApplyFilteredIncludes(options);

var projected = query.ApplySelect(options);

var result = await projected.ToListAsync();
```

---

## Usage Guide

### When to Use These Methods
Use low-level methods when:
- You need a **custom execution flow** (e.g., applying custom filters between steps).
- You want **partial query processing** (e.g., sorting without paging).
- You want to **combine with custom logic** that isn't handled by the standard pipeline.

### When NOT to Use
Avoid using these methods when:
- You want the **full pipeline** executed efficiently → use `ToProjectedQueryResultAsync`.
- You want **validation** included → use `ApplyValidatedQueryOptions`.

### Relationship to High-Level Methods

| Method Type | Example | Purpose |
| :--- | :--- | :--- |
| **Low-level** | `ApplyFilter` | Atomic operation on a single pipeline step. |
| **High-level** | `ApplyQueryOptions` | Combines Filter + Sort + Paging. |
| **Full execution** | `ToProjectedQueryResultAsync` | Executes the entire Filter → Paging → Projection chain. |

> [!WARNING]
> **Important Warning**: These low-level methods do **NOT** perform validation. If used in public APIs, you must apply validation separately using the `QueryValidator` before calling them.

---

## Usage Scenarios

### Scenario 1 — Simple Filtering API
You want to return full entities and ensure the query is valid.
- **Method**: `ApplyValidatedQueryOptions` + `ToListAsync()`

### Scenario 2 — Projection-Enabled API
You want to allow clients to shape the response using `select` or `include`.
- **Method**: Manual `QueryValidator` + `ToProjectedQueryResultAsync`

### Scenario 3 — Full Manual Pipeline
You need to inject custom logic between filtering and projection steps.
- **Method**: `ApplyQueryOptions` → *Custom Logic* → `ApplySelect` → `ToListAsync()`

### Scenario 4 — Advanced Include Filtering
You want to shape related collections without affecting the root result count.
- **Method**: `ApplyQueryOptions` + `ApplyFilteredIncludes` + `ToQueryResultAsync`

---

## Decision Guide

| If you want... | Use this method |
| :--- | :--- |
| **Filtering only** (Full Entities) | `ApplyValidatedQueryOptions` |
| **Filtering + Projection** (Shaped Data) | `ToProjectedQueryResultAsync` |
| **Validation Only** | `QueryValidator` |
| **Metadata** (TotalCount, etc.) | `ToQueryResultAsync` |
| **ASP.NET API** | Always use **Async** variants |

---

## Best Practices

- **Always Validate**: Never expose a public endpoint without running validation, either via `ApplyValidatedQueryOptions` or the manual `QueryValidator`.
- **Avoid Double-Applying**: Never chain `ApplyQueryOptions` with a result wrapper like `ToProjectedQueryResultAsync` using the same `options` object.
- **Async Execution**: Use async methods in ASP.NET Core controllers to maintain application responsiveness and scalability.
- **Prefer Result Wrappers**: Use `ToProjectedQueryResultAsync` for flexible APIs to reduce network payload and improve performance through single-trip SQL queries.

