# End-to-End Execution Benchmarks

Execution benchmarks measure the **complete query pipeline** from query string to materialized objects:

```
Query string  →  Parse  →  Validate  →  Generate Expression  →  Execute (EF Core)  →  Materialize
```

These are the most representative benchmarks for API performance because they include **all** overhead: parsing, expression generation, EF Core translation, database execution (InMemory provider), and object materialization.

> **⚠️ InMemory Provider Caveat**
> These benchmarks use EF Core's **InMemory provider**, not a relational database. InMemory is significantly faster than SQL Server but does not simulate SQL translation overhead, network I/O, or real database query planning. For SQL Server results, see [Database Execution](./database-execution.md).

---

## Dataset

All queries run against a deterministic e-commerce dataset (`Random(42)`):

| Entity | Count | Relationships | Notes |
|:--------|------:|:--------------|:------|
| **Users** | 1,000 | 1:N Orders | 8 cities, 3 status values |
| **Orders** | ~2,500 | 1:N OrderItems, Payments | 4 status values, 0–5 per user |
| **OrderItems** | ~5,000 | N:1 Product | 1–3 per order |
| **Products** | 50 | — | 5 categories |
| **Payments** | ~2,500 | — | 0–2 per order |

All queries use `.AsNoTracking()` to exclude EF Core change-tracking overhead.

---

## Benchmark Results

### Scenario 1: Filter + Sort + Paging

**Query:** `status:eq:active AND age:gt:25 | sort: name ASC | page 1, size 100`

The most common REST API pattern. All libraries implement this scenario.

| Library | Mean | Relative | Allocated | Memory Ratio |
|:---------|-----:|---------:|----------:|-------------:|
| **FlexQuery.NET** | **17.67 ms** | **0.44×** | 21.42 KB | 0.22× |
| **Handwritten LINQ** | 40.21 ms | 1.00× | 97.11 KB | 1.00× |
| **Gridify** | 40.33 ms | 1.00× | 107.76 KB | 1.11× |
| **System.Linq.Dynamic.Core** | 40.95 ms | 1.02× | 110.79 KB | 1.14× |
| **Sieve** | 41.37 ms | 1.03× | 117.67 KB | 1.21× |

**Interpretation:**

1. **FlexQuery.NET is 2.25× faster than handwritten LINQ** in this InMemory scenario. This may seem counterintuitive — FlexQuery constructs an expression tree at runtime while handwritten LINQ is compiled ahead-of-time. The difference arises because:
   - Handwritten LINQ: `_users.Where(u => u.Status == "active" && u.Age > 25)` creates a closure class to capture the string literals `"active"` and `25`
   - FlexQuery.NET: Builds `ConstantExpression` nodes directly without closure overhead
   - EF Core InMemory processes the resulting expression trees differently, and FlexQuery's shape happens to be more optimal for this provider

2. **Gridify and Dynamic.Core** perform similarly to handwritten LINQ. Their deferred parsing does not provide an advantage in end-to-end measurement because parsing cost is dwarfed by materialization.

3. **Sieve shows measurable overhead** (~3% slower). Sieve's attribute-driven architecture scans entity properties via reflection on each request to determine filterable/sortable fields. This constant cost per request adds up.

4. **Memory allocation:** FlexQuery.NET allocates **79% less memory** than handwritten LINQ (21.42 KB vs 97.11 KB). This is significant under high throughput — fewer Gen0/Gen1 collections mean less GC pause time.

---

### Scenario 2: Dynamic Projection (Select Subset of Fields)

**Query:** `status:eq:active | select: id, name, email, city`

Measures runtime projection into anonymous or dynamic objects. Only FlexQuery.NET and Dynamic.Core support fully dynamic projection without compile-time DTOs.

| Library | Mean | Relative | Gen0 | Gen1 | Gen2 | Allocated |
|:---------|-----:|---------:|-----:|-----:|-----:|----------:|
| **Handwritten LINQ** | 50.28 ms | 1.00× | 2400.0 | 1200.0 | 400.0 | 20,659 KB |
| **FlexQuery.NET** | 50.57 ms | 1.01× | 2400.0 | 1200.0 | 400.0 | 20,669 KB |
| **System.Linq.Dynamic.Core** | 52.15 ms | 1.04× | 2500.0 | 1300.0 | 400.0 | 20,934 KB |

**Interpretation:**

1. All libraries perform **within 4%** of each other. Projection is dominated by object materialization cost — the overhead of choosing which fields to include is marginal.

2. **High Gen2 allocations (400 collections)** indicate long-lived objects. This is expected when projecting into anonymous types that must be boxed as `object` and later unboxed during JSON serialization.

3. **Gridify and Sieve are excluded** because their native packages do not support server-side dynamic projection. They require fixed-type `Select()` calls, meaning a code change is needed for each shape.

---

### Scenario 3: Nested Collection Query (`Any`)

**Query:** `orders:any(status:eq:completed)`

Measures correlated subquery execution — finding parent records (`User`) with matching child records (`Order`). Only FlexQuery.NET and handwritten LINQ support this natively among the compared libraries.

| Library | Mean | Relative | Gen0 | Gen1 | Gen2 | Allocated |
|:---------|-----:|---------:|-----:|-----:|-----:|----------:|
| **Handwritten LINQ** | 142.26 ms | 1.00× | 4250.0 | 2500.0 | 750.0 | 34,589 KB |
| **FlexQuery.NET** | 139.27 ms | 0.98× | 4250.0 | 2500.0 | 750.0 | 34,597 KB |

**Interpretation:**

1. **Difference is within margin of error (< 2%)**. Dynamic query overhead vanishes when the database engine (EF Core InMemory) must traverse large object graphs. The cost of evaluating `o => o.Status == "completed"` for every order across every user dominates completely.

2. **Memory allocation is identical** because both queries materialize the same `User` entities with their `Orders` collections. The inner `Any` evaluation is where work happens, but allocation occurs during result construction.

3. **This is the most important scenario for validation:** It proves that for heavy relational queries with nested predicates, the choice of dynamic query library has **no measurable impact** on execution time. You can use FlexQuery.NET without performance penalty for complex filters.

---

## Combined Scenarios

Filter + Sort + Page + Projection is measured in the [API Benchmarks](./api-benchmarks.md) under a full HTTP request, which includes JSON serialization overhead.

---

## Allocation Analysis

### Memory Pressure Comparison

| Scenario | FlexQuery.NET | Handwritten | Gridify | Sieve | Dynamic.Core |
|:----------|--------------:|------------:|--------:|------:|-------------:|
| Filter+Sort+Page | 21.42 KB | 97.11 KB | 107.76 KB | 117.67 KB | 110.79 KB |
| Projection | 20,669 KB | 20,659 KB | N/A | N/A | 20,934 KB |
| Nested Any | 34,597 KB | 34,589 KB | N/A | N/A | N/A |

FlexQuery.NET reduces memory allocation by **78%** for simple queries and matches handwritten for complex projections. Lower allocation means:
- Fewer Gen0 garbage collections under load
- Lower memory bandwidth pressure
- Better cache locality
- More predictable latency under contention

---

## Execution vs. Parsing Trade-off

A common misconception: "FlexQuery is slower because it parses eagerly."

The data shows the opposite in end-to-end measurement. FlexQuery.NET's eager parsing **does not slow down** overall execution because:

1. **Parsing is a tiny fraction of total time** (0.1 µs out of 17–140 ms)
2. **Eager validation enables optimizations** — the AST can be validated and normalized upfront, allowing the expression builder to generate tighter LINQ
3. **Caching everything** — parsed AST + expression tree can be cached; deferred libraries cannot cache intermediate state

Gridify and Sieve appear faster during "parsing" micro-benchmarks because they defer work. But in full execution, that deferred work still happens — it's just hidden inside the `Apply()` call. The net result: Gridify/Sieve show similar or slightly worse end-to-end performance.

---

## Bottom Line

For real-world API scenarios (filter + sort + page), **FlexQuery.NET is the fastest** among the compared dynamic query libraries, and in this InMemory test, it even **outperforms handwritten LINQ**.

TheInMemory provider favors FlexQuery's expression shape. Real SQL Server performance may differ — see [Database Execution](./database-execution.md) for relational database results.
