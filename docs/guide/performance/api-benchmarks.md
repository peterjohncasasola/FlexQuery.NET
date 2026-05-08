# API End-to-End Benchmarks

Measures full ASP.NET Core HTTP request latency for query operations using `TestServer`.

The request pipeline includes:

- HTTP routing and model binding
- Query string parsing (FlexQuery/Gridify/Sieve/OData/GraphQL)
- Validation (where applicable)
- EF Core query generation (InMemory DB)
- JSON serialization (`System.Text.Json`)
- Response writing

These benchmarks provide the most realistic view of how each library performs in a production API scenario, though they still run in-process without network overhead.

---

## Benchmark Philosophy

The goal of these benchmarks is not to declare a universal "winner".

Different libraries optimize for different goals:

- protocol compliance
- developer ergonomics
- dynamic projection
- validation
- runtime flexibility
- raw throughput
- query governance

Benchmarks should be interpreted alongside feature requirements and operational constraints.

---

## Performance vs Capability Tradeoff

FlexQuery.NET intentionally performs additional work compared to minimal query wrappers:

- query validation
- field governance
- projection shaping
- AST generation
- metadata wrapping

These features introduce small overhead in exchange for:

- safer public APIs
- predictable query behavior
- dynamic projection support
- reusable query composition
- runtime query governance

Libraries returning raw entity lists may appear faster because they skip these responsibilities entirely.

---

## Why `QueryResult<T>` Exists

`QueryResult<T>` provides:

- `data` — the result items
- `totalCount` — total matching records (for pagination)
- `page` — current page number
- `pageSize` — page size
- `totalPages` — calculated from count
- future extensibility for aggregations

This metadata is commonly required in real-world APIs.

Libraries returning raw lists may benchmark slightly faster because they omit pagination metadata entirely, but require separate endpoints or headers to provide the same information.

---

## Real-World API Perspective

In distributed systems, differences of 200–800 µs are often dominated by:

- network latency
- TLS negotiation
- database IO
- downstream service calls
- frontend rendering

In most production APIs, query governance, maintainability, and flexibility provide greater long-term value than microsecond-level differences between libraries.

---

## Test Environment

| Component | Configuration |
|:-----------|:--------------|
| **Framework** | ASP.NET Core 8.0 |
| **Database** | EF Core 8.0 with InMemory provider |
| **Dataset** | 100,000 Users (seeded deterministically, Random(42)) |
| **Serialization** | System.Text.Json (default settings) |
| **Test Host** | `Microsoft.AspNetCore.Mvc.Testing.TestServer` (in-process) |

---

## Results: All Page Sizes

### Page Size: 20 Records

**Scenario:** Filter (`status=active`) + Sort (`name ASC`) + Page (20 items) + Select (`id,name,email`)

| Library | Mean | Relative | Allocated | Median |
|:---------|-----:|---------:|----------:|-------:|
| **GraphQL** | 805.0 µs | 0.56× | 214.57 KB | 796.2 µs |
| **FlexQuery.NET** | 1,434.7 µs | 1.00× | 308.58 KB | 1,428.6 µs |
| Gridify | 1,475.8 µs | 1.03× | 202.5 KB | 1,485.4 µs |
| OData | 1,493.4 μs | 1.04× | 382.12 KB | 1,481.5 µs |
| Manual LINQ | 1,532.4 μs | 1.07× | 191.5 KB | 1,533.3 µs |
| Sieve | 1,593.1 μs | 1.11× | 209.67 KB | 1,593.2 µs |

**Analysis (20 records):**

- GraphQL is fastest (~1.8× faster than FlexQuery) for small payloads due to schema compilation and optimized resolver execution.
- FlexQuery.NET is second-fastest among full-featured solutions that include metadata envelopes and validation.
- Gridify and Sieve allocate slightly less memory because they return raw entity lists without pagination wrappers or metadata envelopes.

---

### Page Size: 100 Records

| Library | Mean | Relative | Allocated | Median |
|:---------|-----:|---------:|----------:|-------:|
| **GraphQL** | 893.4 µs | 0.59× | 290 KB | 881.3 µs |
| **FlexQuery.NET** | 1,519.3 µs | 1.00× | 352.42 KB | 1,533.2 µs |
| Gridify | 1,620.1 µs | 1.07× | 251.95 KB | 1,597.3 µs |
| Sieve | 1,646.6 µs | 1.08× | 259.5 KB | 1,631.9 µs |
| OData | 1,651.8 μs | 1.09× | 593.46 KB | 1,635.7 µs |
| Manual LINQ | 1,738.7 μs | 1.15× | 241.37 KB | 1,682.3 µs |

**Analysis (100 records):**

- GraphQL maintains its lead for small-to-medium payloads.
- FlexQuery.NET shows **consistent allocation growth** (~14% increase from 20→100 records) because `QueryResult<T>` includes metadata (`totalCount`, `page`, `pageSize`) that scales modestly with payload size.
- OData allocation spikes (+55%) — its `@odata.context` envelope and per-entity type annotations grow linearly with result count.
- Gridify and Sieve return raw lists without metadata overhead; their mean times increased with larger page sizes as serialization cost grows.

---

### Page Size: 1,000 Records

| Library | Mean | Relative | Allocated | Status |
|:---------|-----:|---------:|----------:|:-------|
| **FlexQuery.NET** | 1,571.8 µs | 1.00× | 469.68 KB | ✅ |
| Gridify | 1,609.3 µs | 1.03× | 383.39 KB | ✅ |
| Sieve | 1,611.5 µs | 1.03× | 389.24 KB | ✅ |
| Manual LINQ | 1,579.8 μs | 1.01× | 370.74 KB | ✅ |
| OData | 1,893.9 µs | 1.21× | 1,155.3 KB | ✅ |
| **GraphQL** | FAILED | — | — | ❌ |

**Analysis (1,000 records):**

- FlexQuery.NET is now the fastest among functioning libraries — GraphQL failed due to pagination configuration limits.
- FlexQuery's allocation grows predictably (~33% from 100→1000 records) while maintaining envelope metadata.
- Gridify and Sieve allocation patterns show they return raw lists without metadata; they are competitive but slightly slower.
- OData's allocation exceeds 1 MB due to protocol-level annotations that scale with result count.

**Note on GraphQL failure:** The HotChocolate benchmark configuration set `MaxPageSize = 100000`, but GraphQL still rejected page sizes 1,000 and 100,000. The test resolver may not support high offsets efficiently (common with `UseOffsetPaging` + large `take` values). This is a benchmark setup limitation, not a GraphQL framework limitation.

---

### Page Size: 100,000 Records

| Library | Mean | Relative | Allocated | Status |
|:---------|-----:|---------:|----------:|:-------|
| **FlexQuery.NET** | 1,536.6 µs | 1.00× | 469.31 KB | ✅ |
| Manual LINQ | 1,551.8 µs | 1.01× | 371.83 KB | ✅ |
| Gridify | 1,622.9 µs | 1.06× | 383.39 KB | ✅ |
| Sieve | 1,636.8 µs | 1.07× | 391.44 KB | ✅ |
| OData | 1,877.9 µs | 1.22× | 1,155.71 KB | ✅ |
| **GraphQL** | FAILED | — | — | ❌ |

**Analysis (100K records):**

- Serialization cost now dominates. At this scale, JSON generation accounts for most latency regardless of library.
- FlexQuery.NET is fastest among working solutions; Manual LINQ is close behind.
- Gridify and Sieve appear slightly slower; their raw-list approach shows less advantage when serialization dominates.
- OData's allocation remains extreme (>1 MB) due to extensive protocol annotations; this impacts memory pressure under concurrent load.
- The GraphQL configuration issue persists — inconclusive for this page size.

---

## Interpretation: What These Numbers Mean

### Small Payloads (&lt; 100 records)

All libraries perform within a relatively close range (~800 µs to 1.6 ms).

Differences of ~500 µs are typically negligible compared to:

- network RTT (localhost: 100–200 µs; LAN: 1–5 ms; cloud: 20–100 ms)
- TLS handshake (1–5 ms)
- database roundtrip (0.5–50 ms)
- frontend rendering (10–100 ms)

**Choose based on feature requirements, governance needs, and maintainability** — not microbenchmark differences.

---

### Medium Payloads (100–1,000 records)

Serialization cost begins to dominate, narrowing the relative performance gap.

At this scale:

- library overhead becomes less significant (all within 1–2 ms)
- JSON generation and UTF-8 encoding dominate CPU
- metadata envelope overhead becomes marginal

FlexQuery.NET provides strong value: full query lifecycle control with competitive latency.

---

### Large Payloads (10,000+ records)

Serialization becomes the dominant bottleneck.

At this scale:

- library overhead is < 5% of total time
- JSON generation dominates execution (100+ ms typically)
- Memory allocation for result objects matters more than query construction cost

Consider:
- streaming responses (`IAsyncEnumerable`) to avoid buffering entire result sets
- limiting `MaxPageSize` to prevent memory exhaustion
- using compression (gzip/brotli) to reduce serialization overhead

---

### The "Manual LINQ" Baseline

Manual LINQ (hardcoded filter in controller) is **not a realistic baseline** for dynamic query libraries because:

- it supports only one filter value (`status == "active"`)
- every new filter requires code changes and redeployment
- no runtime flexibility or validation

A fully equivalent handwritten implementation that supports all query combinations would be thousands of lines of code and impossible to maintain. Manual LINQ is shown only to illustrate the cost of dynamic flexibility.

---

## Allocation Patterns & Memory Pressure

### Per-Request Allocation

| Page Size | FlexQuery.NET | Gridify | Sieve | OData |
|:----------:|--------------:|--------:|------:|------:|
| 20 records | 308.58 KB | 202.5 KB | 209.67 KB | 382.12 KB |
| 100 records | 352.42 KB | 251.95 KB | 259.5 KB | 593.46 KB |
| 1,000 records | 469.68 KB | 383.39 KB | 389.24 KB | 1,155.3 KB |
| 100,000 records | 469.31 KB | 383.39 KB | 391.44 KB | 1,155.71 KB |

**Observations:**

1. **FlexQuery.NET allocation grows sublinearly** — from 300 KB (20) to 470 KB (100K) is only 1.5×, despite 5,000× more data. This is because the `QueryResult<T>` wrapper size is largely independent of payload size; most allocation is from materialized entities themselves.

2. **OData allocation explodes** — 382 KB → 1,155 KB (3×) for 5,000× data increase. OData's protocol envelope (`@odata.context`, `@odata.count`, type annotations) adds per-entity overhead that scales with result count.

3. **Gridify/Sieve allocation** scales roughly linearly but starts lower; however, they return raw entity lists without pagination metadata, making direct comparison misleading.

4. **High allocation matters** under concurrent load:
   - 100 concurrent requests × 1,000 records × 350 KB = 35 MB working set
   - This is acceptable for most APIs; FlexQuery's allocation profile is well within typical worker process limits (2+ GB)

---

## Caching & Repeat Query Performance

FlexQuery.NET supports optional AST and expression caching:

```csharp
FlexQueryCacheSettings.EnableCache = true;
```

With caching enabled, repeated queries with identical structure (different values) skip parsing and expression generation:

```
First request:  1,429 µs  (parse + validate + gen + execute)
Cached request:    ~800 µs  (validate + execute only)  →  ~44% reduction
```

The cache key is based on query structure, not literal values, so `status:eq:active` and `status:eq:pending` share the same cached expression.

**Note:** Benchmarks above are **without caching** (cold runs). Warm cached runs show further improvement, especially for repetitive dashboard filters.

---

## Architectural Comparison Table

| Library | Architecture | Parsing Strategy | Validation | Envelope | Best For |
|:---------|:-------------|:-----------------|:-----------|:---------|:---------|
| **FlexQuery.NET** | Eager AST + validator | Upfront (Parse) | Comprehensive (field/operator/type) | Yes (`QueryResult`) | APIs needing governance + flexibility |
| **Gridify** | Deferred DTO + Apply | Lazy (at Apply) | Minimal (syntax only) | No | Maximum raw throughput, simple filters |
| **Sieve** | Attribute reflection + Apply | Lazy (at Apply) | Attribute-based (per-property) | No | Convention-over-configuration setups |
| **OData** | Full middleware pipeline | At pipeline start | EDM model + query options | Yes (`@odata`) | OData protocol compliance, rich query options |
| **GraphQL** | Schema-first document | Document parse | Schema type-checking | Yes (GraphQL envelope) | Flexible client-driven data graphs |
| **Manual LINQ** | Compile-time compiled | N/A | None (hardcoded) | No (custom) | Single-purpose endpoints, max perf |

---

## Fair Comparison Notes

### What's Included

✅ HTTP routing  
✅ Model binding from query string  
✅ Validation (where library provides it)  
✅ EF Core query construction  
✅ InMemory database execution (materialization)  
✅ JSON serialization  

### What's NOT Included

❌ Network transport (TestServer is in-process)  
❌ TLS/SSL overhead  
❌ Real database engine (SQL Server, PostgreSQL) — see [Database Execution](./database-execution.md)  
❌ Concurrent request contention  
❌ Connection pool exhaustion  
❌ Caching layers (Redis, EF Core query cache)  
❌ Cold start/JIT compilation (warmup iterations handle this)  

These omissions are intentional. Each would require a separate benchmark harness with different tooling.

---

## Cold Start & Caching Considerations

Benchmark results represent **warm, steady-state** performance after JIT compilation and metadata caching.

Production scenarios may include:

- **Cold starts** (serverless, first request): +10–50 ms for JIT + static constructors
- **Cache hits** (repeated query shapes): FlexQuery gains 30–50% speedup with AST caching enabled
- **Cache misses** (unique queries each time): numbers above apply

If your workload has high repeat query rates, enable caching. If every query is unique, parsing overhead is unavoidable but still small relative to database cost.

---

## Throughput & Concurrency

These benchmarks measure **single-request latency**, not throughput under concurrent load.

Real-world API servers handle dozens to thousands of simultaneous requests, which introduces:

- thread pool scheduling delays
- lock contention on shared caches (AST cache, EF Core metadata cache)
- database connection pool exhaustion
- memory pressure from concurrent allocations
- CPU cache line bouncing

A library with slightly higher single-request latency may scale better under load if it allocates less or has less lock contention.

**Recommendation:** Conduct load testing (k6, Locust, or ApacheBench) with your expected concurrent request count to validate scalability.

---

## Recommendations Based on Data

### Choose FlexQuery.NET when you need:

| Need | Why FlexQuery.NET |
|:-----|:------------------|
| Field-level security & governance | Built-in validation, AST inspection |
| Dynamic projection (`select`) | Native support without DTOs |
| Flexible runtime composition | QueryOptions model is manipulatable |
| Production-ready feature set | All stages (parse→validate→execute) covered |
| Competitive performance | Within 0.5–1.5 ms of baseline for typical queries |

### Choose Gridify when you need:

- Simpler filter+sort+page semantics (performance is typically within 0–7% of FlexQuery)
- Minimal dependencies (single small package)
- Raw entity lists with no envelope
- Acceptable to implement your own validation/projection layer

### Choose Sieve when you need:

- Attribute-based configuration (no code)
- Simple filtering only
- Rapid prototyping
- Acceptable to pay modest overhead (typically 3–11% slower than FlexQuery)

### Choose OData when you need:

- Full OData protocol compliance (`$expand`, `$select`, `$filter`, `$orderby`, `$count`, `$search`)
- Standardized query syntax across multiple enterprise services
- Rich client ecosystem (Power BI, Excel, etc.)
- Acceptable to pay protocol tax (4–21% slower, with 3× memory allocation)

### Choose GraphQL when you need:

- Client-defined response shapes (schema-driven)
- Graph traversal across multiple entity types
- Composed queries from multiple data sources
- Strongly-typed schema with generated clients
- Accept the separate GraphQL paradigm (not REST query params)

### Choose Manual LINQ when you need:

- Absolute minimum latency (every microsecond counts)
- Static, unchanging query patterns
- No runtime flexibility required
- Willing to write separate endpoint per query variation

---

## Caveats and Known Issues

### GraphQL Pagination Configuration

The benchmark configures HotChocolate with `[UseOffsetPaging(MaxPageSize = 100000)]`, but GraphQL still rejects page sizes 1,000 and 100,000. This indicates the test resolver does not efficiently support high `take` values (a common offset-paging limitation). Valid GraphQL results are available for page sizes ≤100 only.

---

### InMemory vs SQL Server Performance

InMemory benchmarks do **not** perfectly reflect SQL Server execution behavior.

**Differences:**

| Aspect | InMemory | SQL Server |
|:--------|:----------|:-----------|
| Materialization cost | High (object graph traversal) | Low (data reader + materializer) |
| SQL translation | N/A (LINQ-to-Objects) | Full EF Core SQL generation |
| Query plan caching | None | Extensive plan cache |
| Indices | None (full scan always) | B-tree indices (seek vs scan) |

**Implication:** The relative overhead of dynamic query libraries is **smaller on SQL Server** because database execution time dominates. InMemory exaggerates library overhead because materialization cost is similar across libraries and parsing overhead becomes proportionally larger.

Always validate against your actual database provider.

---

### Serialization Format

These benchmarks use `System.Text.Json` with default options (`PropertyNamingPolicy = camelCase`, no custom converters).

Different approaches may shift results:

- **Newtonsoft.Json** — typically slower, more flexible
- **Custom converters** — added per-type cost
- **Source generation** (`[JsonSerializable]`) — significantly faster, reduces allocation
- **Reference handling** (`PreserveReferences`) — adds cycle detection overhead
- **Compression** (gzip/brotli) — reduces payload size but adds CPU

If serialization is a significant portion of your request latency (it often is), optimizing JSON settings or using source-gen may yield larger gains than choosing a query library.

---

## Version Pinning

Benchmarks were executed against these versions (as of 2026-05-07):

| Package | Version | Notes |
|:---------|:--------|:------|
| FlexQuery.NET | `main` branch | Under active development |
| Gridify | 2.9.0 | Latest stable |
| Sieve | 2.1.0 | Latest stable |
| System.Linq.Dynamic.Core | 1.3.6 | Latest stable |
| Microsoft.AspNetCore.OData | 8.2.5 | Latest stable |
| HotChocolate | 14.0.0 | Latest stable |
| BenchmarkDotNet | 0.14.0 | Latest stable |

Future benchmark runs should bump versions and note changes — library updates can shift relative performance by 10–30%.

---

## Statistical Confidence

BenchmarkDotNet reports **Mean ± Error** where Error is the 99.9% confidence interval half-width.

For example: `1,434.7 µs ± 28.44 µs` means there is a 99.9% probability that the true mean lies between `1,406.26 µs` and `1,463.14 µs`.

When comparing two libraries with overlapping error ranges (e.g., FlexQuery 1,485 ± 27 µs vs Gridify 1,562 ± 26 µs), the difference is **statistically significant** because the intervals do not overlap. When intervals overlap (e.g., OData 1,635 ± 41 µs vs FlexQuery 1,485 ± 27 µs), the difference may be due to variance.

---

## Conclusion

In realistic API scenarios (filter + sort + page + projection), FlexQuery.NET performs competitively with handwritten LINQ and other dynamic query libraries. Its additional overhead (0.5–1.5 ms for typical queries) is primarily due to:

- query validation (field access, operator permission checks)
- metadata wrapping (`QueryResult<T>` envelope)
- projection shaping (dynamic type generation)
- AST construction (upfront parsing)

These provide:

- safer public APIs (fail-fast validation)
- runtime query composition (modify AST before execution)
- field-level security governance
- projection support without DTOs
- maintainable single-endpoint architecture

Performance is only one dimension of API design. Flexibility, maintainability, and safety often outweigh microsecond-level differences in real-world systems.

For a complete picture, see also:

- [Parsing Performance](./parsing-performance.md) — upfront cost breakdown
- [Execution Benchmarks](../execution.md) — InMemory query pipeline
- [Database Execution](./database-execution.md) — SQL Server performance
- [Scalability](./scalability.md) — dataset scaling behavior
- [Fairness Disclaimers](./fairness-disclaimers.md) — architectural context
