
# Performance

FlexQuery.NET is designed for production-oriented APIs that require dynamic querying while remaining compatible with EF Core’s server-side query translation pipeline.

This page explains the architectural decisions behind FlexQuery.NET’s performance characteristics, common tradeoffs, and practical optimization guidance.

---

# Architecture Overview

FlexQuery.NET converts incoming query definitions into LINQ expression trees that EF Core can translate into SQL.

Typical execution flow:

```text
Query Input
    │
    ▼
Parser → QueryOptions
    │
    ▼
Expression Builder
    │
    ▼
LINQ Expression Tree
    │
    ▼
EF Core SQL Translation
    │
    ▼
Database Execution
```

This allows filtering, sorting, projection, grouping, and paging to execute primarily at the database level instead of in application memory.

---

# Server-Side Execution

FlexQuery.NET is designed to avoid client-side evaluation whenever possible.

Operations such as:

- filtering
- sorting
- paging
- projection
- aggregates
- nested collection operators (`any`, `all`)

are translated into LINQ expressions that EF Core can execute server-side.

---

## Example

```csharp
// filter=orders:any:status:eq:shipped
```

Typical SQL shape:

```sql
SELECT *
FROM Users u
WHERE EXISTS (
    SELECT 1
    FROM Orders o
    WHERE o.UserId = u.Id
      AND o.Status = 'shipped'
)
```

The exact SQL depends on:
- EF Core version
- provider (SQL Server, PostgreSQL, etc.)
- database capabilities

---

# Expression Trees Instead of String SQL

FlexQuery.NET does not generate raw SQL strings.

Instead, it generates LINQ expression trees which EF Core translates into parameterized SQL.

Benefits include:

- provider compatibility
- parameterized query generation
- reduced SQL injection risk
- reuse of EF Core query optimizations

---

# Parsing Overhead

Dynamic query systems introduce some parsing and expression-generation overhead compared to fully handwritten LINQ.

Typical overhead sources include:

- parsing filter syntax
- building expression trees
- validation
- projection generation
- aggregate translation

In most API scenarios, this overhead is relatively small compared to:
- database execution time
- network latency
- serialization cost

Actual performance depends heavily on:
- query complexity
- indexing
- projection size
- EF Core provider
- hardware configuration

---

# Validation Performance

Validation operates against parsed query structures and does not require database access.

Typical validation tasks include:

- field allow/block checks
- operator validation
- depth validation
- projection validation

Validation cost is generally proportional to query complexity rather than dataset size.

---

# Expression Caching

FlexQuery.NET can cache the expensive process of parsing query objects and building LINQ Expression trees. This is highly recommended for high-traffic APIs where the same query patterns (even with different values) are reused.

### 1. Global Enable

Enable caching globally in your `Program.cs`:

```csharp
using FlexQuery.NET.Caching;

// Enable the caching engine
FlexQueryCacheSettings.EnableCache = true;

// Optional: Prevent memory leaks in extremely dynamic scenarios
FlexQueryCacheSettings.MaxCacheSize = 5000; 

// Optional: Cache the compiled delegates (useful for LINQ to Objects)
FlexQueryCacheSettings.CacheCompiledLambdas = true;
```

### 2. Per-Query Control

You can override the global setting on individual requests via `QueryOptions`:

```csharp
var options = QueryOptionsParser.Parse(request);

// Force cache for this specific heavy query, even if global cache is off
options.EnableCache = true; 
```

### 3. How it Works

The caching engine generates a stable, canonical key based on the query's structure (fields, operators, logic) but **ignores the literal values**. This means:

- `status:eq:active`
- `status:eq:pending`

Both share the same cached expression tree, with only the parameter values being swapped at execution time. This drastically reduces CPU overhead and heap allocations for repetitive API calls.

---

# Projection Performance

Projection reduces the number of selected columns returned by the database.

Example:

```http
GET /api/users?select=id,name,email
```

Typical SQL behavior:

```sql
SELECT Id, Name, Email
FROM Users
```

instead of:

```sql
SELECT *
FROM Users
```

Projection can help reduce:
- network payload size
- serialization overhead
- memory allocations

especially for wide entities.

---

# COUNT Query Tradeoff

By default, FlexQuery.NET may execute a separate count query when `IncludeCount` is enabled.

Example:

```http
GET /api/users?includeCount=false
```

Disabling count queries may reduce overhead for:
- infinite scrolling APIs
- streaming-style endpoints
- dashboards where totals are unnecessary

---

# Index Recommendations

Database indexing remains one of the most important performance factors.

Recommended indexing patterns:

| Query Pattern | Suggested Index |
| :--- | :--- |
| Equality filters | Index target columns |
| Sorting | Index sorted columns |
| Date ranges | Index date fields |
| Grouping | Index group-by columns |
| Foreign-key filters | Index relationship keys |

---

# String Search Considerations

Operators such as:

```text
contains
```

may generate SQL patterns similar to:

```sql
LIKE '%value%'
```

Depending on the database engine and indexing strategy, this may result in scans rather than index seeks.

For large-scale text search scenarios, consider:
- full-text indexes
- search engines
- provider-specific optimizations

---

# EF Core Translation Considerations

Performance ultimately depends on EF Core’s generated SQL and the underlying database provider.

Some operations naturally translate more efficiently than others.

Examples:

| Operation | Typical Translation |
| :--- | :--- |
| Equality filters | Efficient indexed predicates |
| Range filters | Efficient indexed predicates |
| StartsWith | Often index-friendly |
| Contains | May require scans |
| Any / All | EXISTS subqueries |
| GroupBy | GROUP BY SQL translation |

Actual execution plans depend on:
- indexes
- query shape
- database engine
- statistics
- data distribution

---

# Split Query Optimization

When multiple collection includes are requested, EF Core may generate a single SQL query with many `LEFT JOIN` clauses. This can lead to:

- **Cartesian Explosion**: The number of result rows multiplies with each additional collection.
- **Redundant Data**: Parent entity data is duplicated for every child row, increasing memory and network usage.

FlexQuery.NET supports optional split query execution to mitigate these issues:

```csharp
var result = await _context.Users.FlexQueryAsync(parameters, exec =>
{
    // Force EF Core to execute collection includes as separate SQL queries
    exec.UseSplitQuery = true;
});
```

This internally applies `.AsSplitQuery()` to the pipeline. 

> [!IMPORTANT]
> Split query behavior is intentionally configured **server-side** through `QueryExecutionOptions`. Clients define **what** data they want; the server defines **how** it is retrieved.

### Performance Tradeoffs

- **Pros**: Drastically reduces data redundancy and memory pressure for wide result sets with many collections.
- **Cons**: Requires multiple roundtrips to the database (one per collection). Not always faster for small datasets or low-latency networks.

---

# Benchmarking Philosophy

Performance measurements are highly environment-dependent.

Factors such as:
- hardware
- network latency
- EF Core provider
- database engine
- indexing
- query complexity
- dataset size

can significantly affect results.

Because of this, FlexQuery.NET focuses on:
- efficient expression generation
- server-side execution
- minimizing unnecessary allocations
- leveraging EF Core optimizations

rather than publishing universal latency guarantees.

---

# Tradeoffs

Like all dynamic query systems, FlexQuery.NET involves tradeoffs.

| Tradeoff | Description |
| :--- | :--- |
| Parsing overhead | Additional work compared to handwritten LINQ |
| Expression generation | Dynamic expression construction has runtime cost |
| Complex query trees | Deep nested filters may generate larger SQL |
| Count queries | Optional extra database roundtrip |
| Dynamic flexibility | More runtime behavior than static queries |

In exchange, FlexQuery.NET provides:
- reusable query pipelines
- dynamic filtering
- projection
- grouping
- aggregates
- validation
- field-level restrictions

within a REST-friendly API model.

---

# Final Thoughts

FlexQuery.NET is designed to balance:
- flexibility
- maintainability
- query safety
- REST compatibility
- EF Core integration

rather than optimizing exclusively for microbenchmark scenarios.

For most applications, overall performance will depend far more on:
- database design
- indexing strategy
- query shape
- network conditions

than on the small overhead introduced by query parsing and expression generation.
