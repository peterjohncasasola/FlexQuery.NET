# FlexQuery.NET vs Gridify vs Sieve

## Overview

Modern .NET applications often require dynamic querying capabilities where clients can define filtering, sorting, projection, and pagination via URL parameters. Manually mapping query strings into LINQ expressions is repetitive, error-prone, and difficult to maintain.

Libraries like **Gridify**, **Sieve**, and **FlexQuery.NET** aim to solve this problem by translating string-based input into `IQueryable` expressions. While they share a similar goal, they differ significantly in scope, flexibility, and architecture.

## Why this comparison exists

When evaluating .NET packages for dynamic querying, developers frequently encounter Gridify and Sieve. Both are excellent, popular libraries. This comparison exists to help architects understand the technical differences between a "lightweight mapping tool" (Gridify), an "attribute-driven tool" (Sieve), and a "full query pipeline engine" (FlexQuery.NET), ensuring you pick the right tool for your specific bounded context.

## When to use each

- Use **Gridify** when you need simple, fast filtering and sorting without projection or complex relationships.
- Use **Sieve** when you want strict, attribute-based control over a few specific DTOs.
- Use **FlexQuery.NET** when you are building enterprise data grids, need dynamic runtime projection (SELECT), deep relational filtering (JOINs), or granular role-based field security.

---

## ⚡ Quick Comparison

| Feature | FlexQuery.NET | Gridify | Sieve |
| :--- | :--- | :--- | :--- |
| Filtering | Yes (DSL, JQL, JSON) | Yes | Yes |
| Sorting | Yes | Yes | Yes |
| Paging | Yes | Yes | Yes |
| Projection (Select) | ✅ Yes (Dynamic) | ❌ No | ❌ No |
| Includes / Joins | ✅ Yes | ⚠️ Limited | ❌ No |
| Grouping | ✅ Yes | ❌ No | ❌ No |
| Configuration | None required | Mapper required | Attributes required |
| Pipeline | ✅ Unified | ❌ Split | ❌ Split |

---

## 🧠 Core Philosophy

| Library | Approach |
| :--- | :--- |
| Gridify | Lightweight filtering/sorting helper |
| Sieve | Attribute-based query control |
| FlexQuery.NET | Full query pipeline engine |

---

## 🔍 What is Gridify?

Gridify is a lightweight library focused on converting string expressions into LINQ `Where` and `OrderBy` clauses.

- Uses expression-based filtering
- Supports sorting and paging
- Requires a **mapper** for advanced scenarios

**Best for:** Simple filtering and sorting use cases where the full entity is returned.

---

## 🔍 What is Sieve?

Sieve is an attribute-driven filtering, sorting, and paging library.

- Uses `[Sieve]` attributes on models
- Enforces opt-in queryability at the class level
- Requires decorating domain or DTO models

**Best for:** Controlled environments with strict field exposure where you don't mind coupling your models to a third-party attribute.

---

## 🔍 What is FlexQuery.NET?

FlexQuery.NET is a **unified query pipeline** built on top of `IQueryable` (and ADO.NET via Dapper).

It supports:
- Filtering (DSL, JQL, JSON)
- Sorting
- Projection (dynamic `SELECT` at runtime)
- Includes / joins with nested filtering
- Grouping & Aggregates
- Pagination (Offset and Keyset)

All in a **single method call**.

```http
GET /api/users?filter=Name:contains:John&select=Name,Orders.Status&include=Orders
```

---

## 🔥 Key Differences

### 1. Projection (Major Differentiator)

**Gridify / Sieve** 
- Do not support dynamic projection
- Require manual `.Select(...)` after the library executes
- Load full entities into memory before projection can occur

**FlexQuery.NET** 
- Supports dynamic `select` directly from the HTTP request
- Builds the `Select` expression tree automatically
- Fetches only required columns from the SQL database

👉 **Less data over the wire = better performance.**

---

### 2. Includes & Navigation

**Sieve** - No support for includes.

**Gridify** - Limited support via custom mapping.

**FlexQuery.NET** - Explicit includes, scoped filtering (filtering the child collection of an include), and nested relationship traversal. FlexQuery.NET automatically generates SQL **joins** when filters are applied within an `include` or `select` parameter.

---

### 3. Configuration Overhead

| Library | Configuration |
| :--- | :--- |
| Sieve | High (attributes on every property) |
| Gridify | Medium (mapper classes) |
| FlexQuery.NET | Minimal (inline lambda policy) |

---

### 4. Pipeline Design

**Gridify / Sieve**
```text
Parse → Apply → Manual Projection
```

**FlexQuery.NET**
```text
Parse → Validate → Execute (single pipeline)
```

---

## 📊 Side-by-Side Example

### Scenario
- Filter users where name contains "John"
- Include their orders
- Select only the user's name and the order's status
- Sort by CreatedAt descending

---

### 🟦 FlexQuery.NET

```http
GET /api/users?filter=Name:contains:John&include=Orders&select=Name,Orders.Status&sort=CreatedAt:desc
```

```csharp
var result = await _context.Users.FlexQueryAsync(parameters, options => 
{
    // Security policy enforced inline
    options.AllowedFields = ["Name", "Orders.Status", "CreatedAt"];
});
```

---

### 🟨 Gridify

```csharp
var mapper = new GridifyMapper<User>().GenerateDefaultMap();

var query = _context.Users
    .Include(u => u.Orders)
    .Gridify(queryObj, mapper);

var result = query.Data.Select(u => new
{
    u.Name,
    Orders = u.Orders.Select(o => new { o.Status })
});
```

---

### 🟥 Sieve

```csharp
var query = _context.Users.Include(u => u.Orders);

// Sieve applies the filters/sorts based on attributes
query = _sieveProcessor.Apply(sieveModel, query);

var result = await query.Select(u => new
{
    u.Name,
    Orders = u.Orders.Select(o => new { o.Status })
}).ToListAsync();
```

---

## 💡 Developer Experience

| Aspect | FlexQuery.NET | Gridify | Sieve |
| :--- | :--- | :--- | :--- |
| Setup | Instant | Medium | Heavy |
| Boilerplate | None | Moderate | High |
| API Cleanliness | Unified | Mixed | Split |
| Learning Curve | Low | Low | Medium |

---

## ⚡ Performance

All three libraries use **expression trees**, meaning EF Core can translate the final `IQueryable` into SQL.

However, FlexQuery.NET provides significant mechanical advantages:
- ✅ **Fetches only selected fields:** Because projection is part of the AST, the `SELECT` clause in SQL is narrowed down.
- ✅ **Avoids Cartesian Explosions:** By explicitly shaping data, you avoid pulling massive object graphs into memory.
- ✅ **Keyset Pagination:** FlexQuery supports `cursor` paging for massive datasets, which avoids the `OFFSET` scanning penalties inherent to the other libraries.

---

## 🧨 Final Takeaway

All three libraries solve similar problems—but at different levels of abstraction.

| Library | Scope |
| :--- | :--- |
| Gridify | Basic querying |
| Sieve | Controlled querying |
| FlexQuery.NET | Full enterprise query engine |

👉 FlexQuery.NET provides the most complete solution for modern REST APIs by combining filtering, validation, projection, and relational querying into a single, unified pipeline.
