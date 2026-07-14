# Memory Usage & Allocation Analysis

Understanding memory allocation patterns is critical for high-throughput APIs. This page consolidates memory usage data across all benchmark categories to help you predict memory pressure under load.

---

## Why Memory Allocation Matters

Allocation directly impacts garbage collection (GC) frequency and pause times:

| Allocation Profile | GC Impact | Suitability |
|:-------------------|:----------|:------------|
| **< 50 KB per request** | Minimal Gen0, negligible GC pressure | Excellent for high QPS |
| **50–200 KB per request** | Occasional Gen0 collections | Good — server GC handles this easily |
| **200–500 KB per request** | Regular Gen0, some Gen1 | Moderate — monitor under load |
| **> 500 KB per request** | Gen1/Gen2 pressure, potential stop-the-world pauses | Concerning for sustained load |

**Key insight:** Memory allocation scales with **result count**, not query complexity. A query returning 10,000 records will allocate ~10× more memory than the same query returning 1,000 records, regardless of filter complexity.

---

## Per-Scenario Allocation Breakdown

### Scenario 1: Filter + Sort + Page (20–100 records)

From [Execution Benchmarks](../execution-pipeline.md):

| Page Size | FlexQuery.NET | Handwritten LINQ | Gridify | Sieve | OData |
|:----------:|--------------:|-----------------:|--------:|------:|------:|
| 20 records | 21.42 KB | 97.11 KB | 107.76 KB | 117.67 KB | 382.12 KB |
| 100 records | 21.42 KB | 97.11 KB | 107.76 KB | 117.67 KB | 593.46 KB |
| 1,000 records | 21.42 KB | — | — | — | 1,155.3 KB |

**Observations:**

1. **FlexQuery.NET** maintains **constant allocation** across page sizes for simple queries because the `QueryResult<T>` wrapper size is largely independent of payload size; main allocation comes from materialized entities, which scale linearly.

2. **OData's allocation explodes** — 382 KB → 1,155 KB (3×) for 5,000× data increase due to protocol-level annotations (`@odata.context`, type annotations) that scale per entity.

3. **Gridify/Sieve** appear to allocate less because they return raw entity lists without pagination metadata envelopes. However, they lack built-in total count, page info — you must implement those separately if needed.

---

### Scenario 2: Dynamic Projection

| Library | Mean | Allocated | Gen0 | Gen1 | Gen2 |
|:---------|-----:|----------:|-----:|-----:|-----:|
| Handwritten LINQ | 50.28 ms | 20,659 KB | 2400 | 1200 | 400 |
| **FlexQuery.NET** | 50.57 ms | **20,669 KB** | 2400 | 1200 | 400 |
| System.Linq.Dynamic.Core | 52.15 ms | 20,934 KB | 2500 | 1300 | 400 |

**Key finding:** All libraries show **high Gen2 allocations** (400 collections per 1K operations), indicating long-lived objects. This is expected when projecting into anonymous types that are boxed as `object` and later unboxed during JSON serialization.

**Interpretation:** Projection allocation is dominated by materialized entity objects, not the projection mechanism itself. Differences between libraries are marginal (< 2%).

---

### Scenario 3: Nested Collection (`Any`)

| Library | Mean | Allocated | Gen0 | Gen1 | Gen2 |
|:---------|-----:|----------:|-----:|-----:|-----:|
| Handwritten LINQ | 142.26 ms | 34,589 KB | 4250 | 2500 | 750 |
| **FlexQuery.NET** | 139.27 ms | **34,597 KB** | 4250 | 2500 | 750 |

**Key finding:** Memory allocation is **virtually identical** because both queries materialize the same `User` entities with their `Orders` collections. The overhead of dynamic query construction is negligible compared to materialization cost.

---

## Allocation Per Record

From [Scalability Benchmarks](./scalability.md):

| Dataset Size | Mean | Allocated | Allocation/Record |
|:-------------:|-----:|----------:|------------------:|
| 100 records | 16.76 µs | 28.93 KB | **~290 bytes** |
| 1,000 records | 102.81 µs | 227.08 KB | **~227 bytes** |
| 10,000 records | 2,824.38 µs | 2,413 KB | **~240 bytes** |

**Per-record allocation (~230–290 bytes)** reflects:
- `User` entity object (~80 bytes)
- Reference overhead for string properties
- EF Core tracking metadata (even with `AsNoTracking`, some tracking occurs during materialization)
- JSON serialization buffers (included in API benchmarks)

---

## Concurrent Request Memory Pressure

If 100 concurrent requests each fetch 1,000 records:

```
Memory pressure: 100 × 227 KB = 22.7 MB working set
Gen0 collections: 100 × 24 = 2,400 Gen0 collects/second
Gen1 collections: 100 × 5 = 500 Gen1 collects/second
```

Under .NET's server GC, Gen0 collections are cheap (< 100 µs) and concurrent. Gen1 is also concurrent. **This workload is well within a typical 2 GB worker process limit.**

---

## Concerning Scenarios

### 1. High Concurrency + Large Page Sizes

```
1,000 concurrent requests × 10,000 records each = 240 MB working set + Gen2 GC pressure
```

**Mitigation:**
```csharp
options.MaxPageSize = 1000;  // reasonable limit for most APIs
```

### 2. Unlimited PageSize (Potential OOM)

If one client requests 500,000 records:
- Allocates 5–10 MB per request
- Blocks thread for 10–50 ms
- Triggers Gen2 GC under memory pressure

**Always enforce maximum page sizes.**

---

## GC Collection Patterns

### Gen0 (Short-lived)
- **Frequency:** High, but collections are fast (< 100 µs)
- **Impact:** Minimal — designed for frequent collection
- **Triggered by:** Per-request allocations that die within the same request

### Gen1 (Medium-lived)
- **Frequency:** Moderate
- **Impact:** Small pause (100–500 µs)
- **Triggered by:** Objects surviving one Gen0 collection

### Gen2 (Long-lived / Heap Fragmentation)
- **Frequency:** Rare, but stop-the-world
- **Impact:** Milliseconds (10–100 ms pause)
- **Triggered by:** Large object allocations or long-lived reference chains

**Warning:** Gen2 collections per operation indicate potential **heap fragmentation risk**. Under continuous load, this could trigger stop-the-world GC pauses.

From scalability benchmarks: 10,000 records show **27.34 Gen2 collections per operation**. This indicates objects surviving multiple GC cycles — monitor this in production.

---

## Memory Growth Under Load

### Linear vs Sublinear Scaling

FlexQuery.NET allocation grows **sublinearly** from page size 20 → 100,000 records (only 1.5× increase) because the `QueryResult<T>` wrapper size is largely independent of payload size.

However, **total working set** still scales with result count because entity materialization dominates allocation.

### Allocation Efficiency Comparison

| Library | 20 records | 100 records | 1,000 records | 100,000 records |
|:--------|:-----------|:------------|:--------------|:----------------|
| FlexQuery.NET | 308.58 KB | 352.42 KB | 469.68 KB | 469.31 KB |
| Gridify | 202.5 KB | 251.95 KB | 383.39 KB | 383.39 KB |
| Sieve | 209.67 KB | 259.5 KB | 389.24 KB | 391.44 KB |
| OData | 382.12 KB | 593.46 KB | 1,155.3 KB | 1,155.71 KB |
| Manual LINQ | 191.5 KB | 241.37 KB | 370.74 KB | 371.83 KB |

**Observations:**
- FlexQuery.NET allocation increases **sublinearly** at large scale due to envelope metadata amortization
- OData allocation **explodes** due to per-entity protocol annotations
- Gridify/Sieve allocation scales linearly but starts lower because they return raw lists without metadata

---

## Practical Recommendations

### 1. Set Maximum Page Size

```csharp
services.AddFlexQuery(options =>
{
    options.MaxPageSize = 1000;   // Prevent memory exhaustion
    options.MaxRecords = 10000;   // Absolute hard limit
});
```

### 2. Use Streaming for Bulk Exports

For endpoints exporting all matching records:

```csharp
[HttpGet("export")]
public async IAsyncEnumerable<object> Export([FromQuery] FlexQueryParameters parameters)
{
    await foreach (var item in _context.Users
        .FlexQueryStreaming(parameters))
    {
        yield return item;
    }
}
```

Streaming avoids buffering entire result sets in memory.

### 3. Monitor GC Pressure in Production

Use `dotnet-counters` or Application Insights to track:
- **Gen0 collection rate** — high rate indicates short-lived allocation, usually fine
- **Gen2 collection rate** — should be near zero; frequent Gen2 indicates memory pressure
- **Heap size** — watch for unbounded growth

### 4. Enable Caching for Repeated Queries

```csharp
FlexQueryCacheSettings.EnableCache = true;
```

Cached queries skip parsing and expression generation, reducing per-request allocation by ~40–50% for repeated query shapes.

### 5. Consider Compression for Large Payloads

For responses > 100 KB:
- Enable gzip/brotli compression
- Reduces serialization overhead and network transfer time
- Trade-off: additional CPU for compression/decompression

---

## Memory vs. Database Trade-off

**Important:** Database I/O (500 µs – 50 ms) dominates memory allocation cost (0.5–2 ms). Even with high allocation, FlexQuery.NET's total request time is typically database-bound.

**Exception:** In-memory filtering scenarios (no database) show allocation as a larger portion of total time. For those cases, consider:
- Reducing page sizes
- Using `AsNoTracking()` (already default in benchmarks)
- Projecting only required fields (`select:`)

---

## Related Pages

- [Scalability](./scalability.md) — How performance scales with dataset size
- [Execution Benchmarks](../execution-pipeline.md) — Full pipeline including allocation numbers
- [API Benchmarks](./api-benchmarks.md) — Real-world HTTP request memory usage
- [Database Execution](./database-execution.md) — SQL Server memory characteristics
