# Performance

FlexQuery.NET is designed for production-scale APIs. This page covers how it performs, where overhead exists, and how to optimize for high-throughput scenarios.

---

## Architecture: Why It's Fast

### Expression Tree Compilation

Every filter, sort, and projection operation is compiled into a **LINQ expression tree**.

Expression trees are:

- Compiled once per unique query shape (with caching enabled)
- Translated to parameterized SQL by EF Core — no string concatenation
- Executed entirely server-side — no client-side evaluation

```
Filter DSL string
      │
      ▼
  DslParser → AST (FilterGroup)
      │
      ▼
  ExpressionBuilder → LINQ Expression Tree
      │
      ▼
  EF Core → Parameterized SQL
      │
      ▼
  Database → Result Set
```

### No Client-Side Evaluation

All operators — including `any`, `all`, `contains`, `between`, and `in` — generate server-side SQL.

```csharp
// filter=orders:any:status:eq:shipped
// Generates:
SELECT * FROM Users u WHERE EXISTS (
  SELECT 1 FROM Orders o WHERE o.UserId = u.Id AND o.Status = 'shipped'
)
```

This means **no data is loaded into memory** before filtering.

---

## Benchmark Goals

FlexQuery.NET targets performance **parity with hand-written EF Core queries** for typical filter/sort/page operations.

| Scenario | Target Latency | Notes |
| :--- | :--- | :--- |
| Simple filter + page | ~2-5ms | Equivalent to hand-written EF Core |
| Complex nested filter | ~5-15ms | Depends on index coverage |
| Projection (5-10 fields) | ~3-8ms | SELECT column list narrowed in SQL |
| Filter + include | ~5-20ms | JOIN per include navigation |
| GroupBy + aggregate | ~5-20ms | GROUP BY translated to SQL |

> [!NOTE]
> Latency numbers are for a local SQL Server with indexed columns. Production latency depends on database size, indexing, network, and server load.

---

## Parsing Overhead

Parsing a query string into `QueryOptions` is fast — typically under **1ms** for typical inputs.

| Format | Relative Parse Cost |
| :--- | :--- |
| DSL (`filter=status:eq:active`) | ~0.1ms |
| JQL (`query=status = "active"`) | ~0.5ms |
| JSON filter | ~0.3ms |
| Indexed format | ~0.2ms |

The JQL format has slightly higher overhead due to recursive descent parsing.

---

## Validation Overhead

Validation runs against the parsed AST — it does **not** touch the database.

- Simple field access check: **< 0.1ms**
- Full validation pipeline (fields, operators, depth): **< 1ms**

The field access validator uses a `ConcurrentDictionary` normalization cache to minimize repeated string allocations.

---

## Expression Caching

FlexQuery.NET supports expression caching to eliminate re-compilation overhead on repeated queries.

Enable it per-request:

```csharp
options.EnableCache = true;
```

Or enable globally via `QueryExecutionOptions`.

**Cache key generation:**

The cache key is derived from:
- Entity type name
- Operation (predicate, projection)
- Case sensitivity setting
- Normalized filter AST hash

The **FilterNormalizer** canonicalizes the AST before hashing — so `status:eq:active AND age:gte:18` and `age:gte:18 AND status:eq:active` produce the **same cache key**.

```csharp
var key = options.GetCacheKey(typeof(User), "predicate");
// e.g., "predicate:MyApp.Entities.User:ci:abc123ef"
```

---

## COUNT Query Optimization

By default, `FlexQueryAsync` runs a `COUNT(*)` query for `totalCount`.

This is a separate SQL trip. For high-frequency endpoints where count is not needed:

```
GET /api/users?filter=status:eq:active&includeCount=false
```

```csharp
// Or in code
options.IncludeCount = false;
```

---

## Projection Performance

Projection (via `ApplySelect`) reduces the SQL column list:

```sql
-- Without projection (all columns)
SELECT Id, Name, Email, PasswordHash, Status, Age, ... FROM Users

-- With select=id,name,email
SELECT Id, Name, Email FROM Users
```

This reduces:
- Network bandwidth (SQL Server → app server)
- Serialization cost (fewer properties)
- Memory allocation (smaller objects)

---

## Index Recommendations

For best performance, index the fields your clients will filter and sort on most.

| Query Pattern | Index Recommendation |
| :--- | :--- |
| `status:eq:active` | Index on `Status` |
| `createdAt:gte:2024-01-01` | Index on `CreatedAt` |
| `name:contains:alice` | Full-text index or accept scan |
| `sort=createdAt:desc` | Index on `CreatedAt DESC` |
| `groupBy=status` | Index on `Status` |

> [!TIP]
> The `contains` operator maps to `LIKE '%value%'` which cannot use a B-tree index. For full-text search, use a full-text index and a custom resolver.

---

## EF Core Tradeoffs

| Feature | EF Core Translation | Notes |
| :--- | :--- | :--- |
| Simple eq/neq/gt/lt | ✅ Full SQL | Fully server-side |
| `contains` | ✅ LIKE '%val%' | Server-side, no index |
| `startswith` | ✅ LIKE 'val%' | Server-side, index-friendly |
| `in` / `notin` | ✅ SQL IN list | Server-side |
| `between` | ✅ BETWEEN | Server-side |
| `any` / `all` | ✅ EXISTS subquery | Server-side |
| Collection aggregate sort | ✅ Subquery | Server-side |
| Projection | ✅ SELECT columns | Server-side |
| GroupBy + aggregate | ✅ GROUP BY | Server-side |

---

## Benchmark Table (Indicative)

Measured on a 100,000 row SQL Server table with standard indexing.

| Query | FlexQuery.NET | Hand-Written EF Core | OData |
| :--- | :--- | :--- | :--- |
| Simple filter + page | ~4ms | ~3ms | ~6ms |
| Filter + sort + page | ~5ms | ~4ms | ~8ms |
| Filter + project | ~6ms | ~5ms | ~10ms |
| Nested any filter | ~7ms | ~6ms | ~12ms |
| GroupBy + count | ~8ms | ~7ms | N/A |

FlexQuery.NET adds **~1-2ms overhead** over hand-written queries, primarily from expression tree compilation (mitigated by caching).

---

## Honest Tradeoffs

| Tradeoff | Detail |
| :--- | :--- |
| **Parse overhead** | ~0.1-0.5ms per request for DSL/JQL parsing |
| **Expression compilation** | ~0.5-2ms first compile; ~0ms with cache |
| **COUNT query** | Extra database trip unless disabled |
| **`contains` operator** | No index support — table scan on unindexed columns |
| **Deep filter nesting** | Complex nested ANDs/ORs may generate large SQL predicates |

These tradeoffs are **transparent and predictable**. For the vast majority of API use cases, the overhead is negligible and the productivity gain is substantial.
