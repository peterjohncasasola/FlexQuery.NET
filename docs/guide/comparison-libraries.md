# FlexQuery.NET vs Gridify vs Sieve: A Practical Comparison

## 💡 Introduction

Modern .NET applications often require dynamic querying capabilities
where clients can define filtering, sorting, projection, and pagination
via URL parameters.

Manually mapping query strings into LINQ expressions is repetitive,
error-prone, and difficult to maintain.

Libraries like **Gridify**, **Sieve**, and **FlexQuery.NET** aim to
solve this problem by translating string-based input into `IQueryable`
expressions.

While they share a similar goal, they differ significantly in **scope,
flexibility, and architecture**.

------------------------------------------------------------------------

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

------------------------------------------------------------------------

## 🧠 Core Philosophy

| Library | Approach |
| :--- | :--- |
| Gridify | Lightweight filtering/sorting helper |
| Sieve | Attribute-based query control |
| FlexQuery.NET | Full query pipeline engine |

------------------------------------------------------------------------

## 🔍 What is Gridify?

Gridify is a lightweight library focused on converting string
expressions into LINQ `Where` and `OrderBy` clauses.

-   Uses expression-based filtering
-   Supports sorting and paging
-   Requires a **mapper** for advanced scenarios

**Best for:** Simple filtering + sorting use cases.

------------------------------------------------------------------------

## 🔍 What is Sieve?

Sieve is an attribute-driven filtering, sorting, and paging library.

-   Uses `[Sieve]` attributes on models
-   Enforces opt-in queryability
-   Requires decorating domain or DTO models

**Best for:** Controlled environments with strict field exposure.

------------------------------------------------------------------------

## 🔍 What is FlexQuery.NET?

FlexQuery.NET is a **unified query pipeline** built on top of
`IQueryable`.

It supports:

-   Filtering (DSL, JQL, JSON)
-   Sorting
-   Projection (dynamic select)
-   Includes / joins
-   Grouping
-   Pagination

All in a **single method call**.

``` http
GET /api/users?filter=Name:contains:John&select=Name,Orders.Status&include=Orders
```

------------------------------------------------------------------------

## 🔥 Key Differences

### 1. Projection (Major Differentiator)

**Gridify / Sieve** - Do not support dynamic projection - Require manual
`.Select(...)` - Load full entity before projection

**FlexQuery.NET** - Supports dynamic `select` - Builds expression tree
automatically - Fetches only required columns

👉 Less data = better performance

------------------------------------------------------------------------

### 2. Includes & Navigation

**Sieve** - No support for includes

**Gridify** - Limited support via mapping

**FlexQuery.NET** - Explicit includes - Scoped filtering (Joins with filters) - Nested relationships

👉 FlexQuery.NET automatically generates SQL **joins** when filters are applied within an `include` or `select` parameter.


------------------------------------------------------------------------

### 3. Configuration Overhead

| Library | Configuration |
| :--- | :--- |
| Sieve | High (attributes) |
| Gridify | Medium (mapper) |
| FlexQuery.NET | Minimal |

------------------------------------------------------------------------

### 4. Pipeline Design

**Gridify / Sieve**

``` text
Parse → Apply → Manual Projection
```

**FlexQuery.NET**

``` text
Parse → Validate → Execute (single pipeline)
```

------------------------------------------------------------------------

## 📊 Side-by-Side Example

### Scenario

-   Filter users where name contains "John"
-   Include orders
-   Select name + order status
-   Sort by CreatedAt

------------------------------------------------------------------------

### 🟦 FlexQuery.NET

``` http
GET /api/users?filter=Name:contains:John&include=Orders&select=Name,Orders.Status&sort=CreatedAt:desc
```

``` csharp
var result = await _context.Users
    .FlexQueryAsync(parameters);
```

------------------------------------------------------------------------

### 🟨 Gridify

``` csharp
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

------------------------------------------------------------------------

### 🟥 Sieve

``` csharp
var query = _context.Users.Include(u => u.Orders);

query = _sieveProcessor.Apply(sieveModel, query);

var result = await query.Select(u => new
{
    u.Name,
    Orders = u.Orders.Select(o => new { o.Status })
}).ToListAsync();
```

------------------------------------------------------------------------

## 💡 Developer Experience

| Aspect | FlexQuery.NET | Gridify | Sieve |
| :--- | :--- | :--- | :--- |
| Setup | Instant | Medium | Heavy |
| Boilerplate | None | Moderate | High |
| API Cleanliness | Unified | Mixed | Split |
| Learning Curve | Low | Low | Medium |

------------------------------------------------------------------------

## ⚡ Performance

All three libraries use **expression trees**, meaning EF Core can
translate queries into SQL.

However, FlexQuery.NET provides advantages:

-   ✅ Fetches only selected fields
-   ✅ Avoids unnecessary data loading
-   ✅ Optional total count (skip expensive queries)

------------------------------------------------------------------------

## 🎯 When to Choose Each

### Use Gridify when:

-   You need simple filtering/sorting
-   You prefer lightweight tools
-   No projection required

------------------------------------------------------------------------

### Use Sieve when:

-   You want strict attribute control
-   You prefer explicit opt-in fields

------------------------------------------------------------------------

### Use FlexQuery.NET when:

-   You need projection, includes, grouping
-   You want a unified pipeline
-   You want minimal setup
-   You want dynamic APIs without DTO explosion

------------------------------------------------------------------------

## 🧨 Final Takeaway

All three libraries solve similar problems --- but at different levels.

| Library | Scope |
| :--- | :--- |
| Gridify | Basic querying |
| Sieve | Controlled querying |
| FlexQuery.NET | Full query engine |

👉 FlexQuery.NET provides the most complete solution for modern APIs by
combining filtering, projection, and relational querying into a single
pipeline.
