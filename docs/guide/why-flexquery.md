# Why FlexQuery.NET?

FlexQuery.NET exists because **dynamic querying in REST APIs is hard** — and most teams solve it wrong.

---

## The Problem

Your API clients want to filter, sort, page, and select fields dynamically.

Without a query abstraction layer, you end up writing this:

```csharp
// ❌ Before FlexQuery.NET — manual, brittle, insecure

[HttpGet("customers")]
public async Task<IActionResult> GetCustomers(
    string? name,
    string? status,
    decimal? minSalary,
    string? sortBy,
    bool sortDesc = false,
    int page = 1,
    int pageSize = 20)
{
    var query = _context.Customers.AsQueryable();

    if (!string.IsNullOrEmpty(name))
        query = query.Where(c => c.Name.Contains(name));

    if (!string.IsNullOrEmpty(status))
        query = query.Where(c => c.Status == status);

    if (minSalary.HasValue)
        query = query.Where(c => c.Salary >= minSalary.Value);

    if (sortBy == "name") query = sortDesc
        ? query.OrderByDescending(c => c.Name)
        : query.OrderBy(c => c.Name);

    // ... repeated for every field

    var total = await query.CountAsync();
    var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    return Ok(new { data, total, page, pageSize });
}
```

This approach has **serious problems**:

- **Every new filter requires code changes** — it does not scale.
- **Sorting is a switch statement** — every new field doubles the code.
- **No validation** — a client can submit anything; you must protect every field manually.
- **No projection** — you always return the full entity, wasting bandwidth.
- **No reuse** — every endpoint reimplements the same boilerplate.

---

## The Solution

FlexQuery.NET replaces all of that with a **query abstraction layer**:

```csharp
// ✅ After FlexQuery.NET — declarative, safe, composable

[HttpGet("users")]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "id", "name", "email", "status", "age", "createdAt"
        };
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

The client can now filter, sort, page, and project **any allowed field** without you writing a single extra line of code.

---

## What FlexQuery.NET Does for You

### 1. Parses Client Input Safely

It accepts query parameters in multiple formats and converts them into a structured, validated AST — `QueryOptions`.

```
GET /api/customers?filter=status:eq:active&sort=name:asc&page=1&pageSize=20&select=id,name,email
```

### 2. Validates Before Execution

Before touching the database, FlexQuery.NET checks:

- Are the fields allowed?
- Are the operators valid for those field types?
- Does the field depth exceed your configured limit?
- Are there nested paths the client shouldn't access?

### 3. Builds Expression Trees

The validated options are compiled into LINQ expression trees. EF Core translates them directly to SQL. No client-side evaluation.

### 4. Returns Shaped Results

The response is projected, paged, and wrapped in a consistent `QueryResult<T>` envelope.

---

## Before vs. After

### Filtering

**Before:**
```csharp
if (!string.IsNullOrEmpty(name))
    query = query.Where(u => u.Name.Contains(name));
```

**After:**
```
GET /api/customers?filter=name:contains:alice
```

### Sorting

**Before:**
```csharp
if (sortBy == "name")
    query = desc ? query.OrderByDescending(u => u.Name) : query.OrderBy(u => u.Name);
```

**After:**
```
GET /api/customers?sort=name:asc,city:desc
```

### Projection

**Before:**
```csharp
// You always return the full User entity — 30+ fields the client may not need
return Ok(users);
```

**After:**
```
GET /api/customers?select=id,name,email
```

The response only contains the 3 requested fields. Smaller payload. Faster response.

---

## The Query Abstraction Layer Philosophy

FlexQuery.NET treats your API as a **composable query engine**, similar to how GraphQL treats your API as a type graph.

Key principles:

- **The server controls what is queryable.** Clients pick from what you expose.
- **Every query is validated before execution.** Nothing unsafe reaches the database.
- **The pipeline is composable.** You can intercept at any step.
- **The output is predictable.** Every endpoint returns the same `QueryResult` envelope.

This makes your API surface stable and secure, even as client requirements evolve.

---

## When to Use FlexQuery.NET

✅ **Use it when:**

- You build **data-heavy APIs** where clients need flexible filtering and sorting.
- You expose **admin panels, dashboards, or reporting endpoints**.
- You want to **reduce the number of custom endpoints** by making one endpoint smart.
- You need **field-level security** without writing custom middleware for every route.
- You are building a **multi-tenant SaaS** where different users see different fields.

---

## When NOT to Use FlexQuery.NET

❌ **Avoid it when:**

- You have a **simple CRUD API** with 2-3 fixed endpoints — the overhead is unnecessary.
- Your endpoint has **fixed, hardcoded query logic** that will never change.
- You are **not using IQueryable** — FlexQuery.NET is designed for `IQueryable` providers (EF Core, Dapper with IQueryable adapters, etc.).
- You need **complex cross-entity JOINs** as your primary query model — consider OData or GraphQL for deeply relational scenarios.

---

## Real-World Pain Points Solved

| Pain Point | How FlexQuery.NET Solves It |
| :--- | :--- |
| Dozens of query parameters per endpoint | One `FlexQueryParameters` DTO handles all |
| SQL injection via dynamic LINQ | Expression-tree based — no string concatenation |
| Over-fetching full entities | `select` parameter projects only requested fields |
| Inconsistent pagination envelopes | Every result is a standardized `QueryResult<T>` |
| No field-level access control | `AllowedFields`, `BlockedFields`, `[FieldAccess]` attribute |
| Boilerplate filter code per field | Zero-boilerplate: declare allowed fields, done |
| Untested query paths | Structured validation with `ValidationResult` errors |
