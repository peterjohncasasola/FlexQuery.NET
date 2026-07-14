# Benchmark Interpretation Guide

This guide helps you translate benchmark numbers into decisions for your project.

---

## Quick Decision Matrix

| Your Priority | Recommended Choice | Expected Overhead vs Handwritten |
|:---------------|:-------------------|:---------------------------------|
| **Maximum flexibility + governance** | FlexQuery.NET | +0.5–5% |
| **Minimum latency for simple filter+page** | Gridify (no wrapper) OR handwritten | 0–5% |
| **OData protocol compliance** | OData | +400–600% |
| **Schema-driven with client-shaped responses** | GraphQL (HotChocolate) | Variable — depends on resolver design |
| **Zero-code attribute config** | Sieve | +20–70% |
| **Nested collection filters (`Any`/`All`)** | FlexQuery.NET only | No overhead — database dominates |

For detailed numbers, see:
- [Execution Benchmarks](../execution-pipeline.md)
- [API Benchmarks](./api-benchmarks.md)
- [Database Execution](./database-execution.md)

---

## Understanding Relative Performance

### "Relative" Column Meaning

| Relative Value | Interpretation |
|:---------------:|:---------------|
| 0.44× | 56% faster than baseline |
| 1.00× | Same as baseline |
| 1.05× | 5% slower than baseline |
| 5.77× | 477% slower than baseline |

**Important:** Baseline varies by benchmark:
- Parsing: FlexQuery.NET is baseline (1.00×), Gridify/Sieve appear faster (0.03–0.04×) because they do less work
- Execution: Handwritten LINQ is baseline (1.00×)
- API: FlexQuery.NET is baseline for its category (varies by page size)

Never compare relative values **across benchmark categories** — they use different baselines.

---

## Reading the Tables

Each benchmark table includes:

| Column | Meaning |
|:--------|:--------|
| **Mean** | Average execution time (after warmup) |
| **Error** | 99.9% confidence interval half-width |
| **StdDev** | Standard deviation across iterations |
| **Ratio** | Relative to baseline (mean ÷ baseline mean) |
| **Gen0/1/2** | Garbage collections per 1000 operations |
| **Allocated** | Heap memory allocated per operation |

### Confidence Intervals

If `Mean = 100 µs ± 2 µs`, the true mean lies between 98–102 µs with 99.9% probability. Overlapping error bars → results are **statistically indistinguishable**.

### GC Collections

Gen0: Short-lived objects (cleared quickly)
Gen1: Medium-lived (survived one GC)
Gen2: Long-lived (survived multiple GCs)

High Gen2 count per operation indicates memory pressure that can trigger full stop-the-world GC pauses in the application.

---

## Scenario-Based Interpretation

### Scenario: Building a Public REST API for Dashboard Queries

**Requirements:**
- Filter on any field
- Sort ascending/descending
- Page 20–100 records
- Secure — users can only see their own tenant's data
- Must handle 1,000 QPS

**Benchmark guidance:**

1. **End-to-End Execution benchmarks** are most relevant — they include full pipeline.
2. Focus on **allocation**, not just mean time. High allocation → more GC → inconsistent latency under load.
3. **FlexQuery.NET 0.44× relative** means it's actually faster than handwritten LINQ in InMemory test. Real SQL will be ~1.05×, still within margin.
4. **Memory at 100 records: 352 KB per request × 1,000 concurrent = 352 MB** — acceptable.
5. **Total latency estimate:** 1.6 ms (library) + 1–5 ms (SQL Server) + 0.5–2 ms (serialization) = **3–8 ms typical**.

**Decision:** FlexQuery.NET is appropriate. Governance features add minimal overhead.

---

### Scenario: Internal Microservice with Strict Latency SLO (p99 &lt; 2 ms)

**Requirements:**
- Simple equality filters only (status, type)
- No dynamic projection
- Must meet 2 ms p99 SLA
- 500 QPS average, 5,000 QPS burst

**Benchmark guidance:**

1. **Database Execution benchmarks** are most relevant — real SQL latency.
2. Handwritten LINQ: 485 µs ± 12 µs
3. FlexQuery.NET: 502 µs ± 15 µs
4. Overhead: 17 µs (3.5%) → well within 2 ms budget
5. But wait: this is **mean**, not p99. Check `StdDev`: ±1.1 µs for handwritten, ±1.4 µs for FlexQuery. p99 ≈ mean + 3σ = ~489 µs vs 507 µs.

**Risk:** The 17 µs overhead is small, but under burst load, JIT compilation, or cold cache, it could push tail latency.

**Decision:** If you need absolute minimum latency, handwritten LINQ wins by a hair. If you need any flexibility (multiple filter values, future fields), FlexQuery's 17 µs cost is justified.

---

### Scenario: Bulk Export Endpoint (CSV of 100,000 records)

**Requirements:**
- Export all matching records
- No paging — single streaming response
- Must not OOM the server
- Client waits 30+ seconds acceptable

**Benchmark guidance:**

1. **Scalability benchmarks** show:
   - 10,000 records → 2,824 µs (InMemory) but this is full table scan
   - With proper indexing, SQL Server 100K records ≈ 500 µs fetch + **serialization dominates**
2. **Serialization cost** is not in most benchmarks. 100K JSON objects ≈ 100–500 ms depending on field count.
3. **Memory per record:** ~240 bytes → 100K records = 24 MB working set just for entities.
4. Recommendation: **Stream response** (`IAsyncEnumerable`) to avoid buffering entire set in memory.

**Decision:** Library choice is secondary to delivery mechanism. Use streaming regardless of library. FlexQuery's allocation efficiency helps but won't solve the fundamental scaling issue.

---

### Scenario: Multi-Tenant SaaS with Per-Tenant Field Restrictions

**Requirements:**
- Tenants can only filter on their own allowed fields
- Some fields restricted by role
- Audit log of every query's AST for compliance

**Benchmark guidance:**

1. **Governance features** are unique to FlexQuery.NET. Gridify/Sieve have no built-in field allowlist at parse time.
2. Overhead of validation: included in end-to-end benchmarks. FlexQuery still wins or ties.
3. **AST caching** can reduce per-request cost after first identical query.
4. You cannot implement field-level security as efficiently with other libraries without writing your own parser/validator.

**Decision:** FlexQuery.NET is the only suitable choice. The governance overhead is already measured and found negligible.

---

## What "Not Benchmarkable" Looks Like

Some aspects resist microbenchmarking:

### Concurrency Contention

Single-threaded benchmarks cannot measure:
- Lock contention on shared AST cache
- Database connection pool exhaustion
- Thread pool starvation under async overload

**How to test:** Use `dotnet-counters` or `dotnet-trace` under load (k6, WRK, ApacheBench). Measure p50/p95/p99, not just mean.

### Cold Start

JIT compilation and static constructors add ~10–50 ms on first request. Benchmarks skip this via warmup iterations.

**Impact:** Serverless functions (Lambda, Azure Functions) pay cold start every invocation. For serverless, consider:
- Pre-warmed instances
- AOT compilation (`PublishAot=true`)

---

## Benchmark Confidence Levels

| Benchmark Category | Confidence | Why |
|:-------------------|:-----------|:----|
| **Parsing** | High | Tight loop, deterministic, no external I/O |
| **Expression Generation** | High | Pure CPU, in-memory |
| **End-to-End Execution (InMemory)** | Medium | InMemory provider ≠ SQL Server; materialization differs |
| **Database Execution (SQL Server)** | High | Real database, but single-threaded; no concurrent load |
| **API End-to-End** | Medium | TestServer in-process; no network; serialization included |
| **Scalability** | High | Clear dataset controls; linear scaling expected |

---

## Red Flags in Benchmark Claims

When evaluating any benchmark (including ours), watch for:

1. **Cherry-picked scenarios** — only showing where one library wins
2. **Unfair configuration** — Gridify with `ApplyAll` disabled, OData without `$select`
3. **Missing baseline** — no handwritten LINQ for context
4. **Small dataset** — 10 records exaggerates overhead (1 µs looks huge relative to 10 µs total)
5. **No error bars** — single run, no statistical rigor
6. **No reproducibility** — secret data generation, hidden commands
7. **Comparing different features** — FlexQuery projection vs Gridify no-projection

Our benchmarks commit to:
- ✅ All scenarios documented with full code
- ✅ Deterministic dataset (Random(42))
- ✅ Statistical reporting (mean ± error, stddev)
- ✅ Fair configuration (library defaults, no sabotage)
- ✅ Complete source available

---

## Applying Benchmarks to Your Context

### Step 1: Identify Your Bottleneck

Profile your production API:
- **Database time > 80%?** → Library choice matters little. Index queries.
- **Serialization > 50%?** → Optimize DTOs, use source-gen, compress.
- **Parsing overhead > 10%?** → Enable caching, simplify query DSL.
- **Allocation GC > 20% CPU?** → Tune pageSize, use streaming.

Benchmarks can only guide you if you know where your time goes.

---

### Step 2: Match Benchmark to Your Stack

| Your Stack | Most Relevant Benchmark |
|:------------|:-----------------------|
| EF Core + SQL Server | [Database Execution](./database-execution.md) |
| EF Core + InMemory (tests) | [End-to-End Execution](../execution-pipeline.md) |
- ASP.NET Core REST API | [API Benchmarks](./api-benchmarks.md) |
- Microservices with large payloads | [Scalability](./scalability.md) |
- Need nested `Any` / `All` | [Execution: Nested](../execution-pipeline.md#scenario-3-nested-collection-queries-any) |

---

### Step 3: Adjust for Your Data Shape

Benchmark dataset: 1,000 users, 2,500 orders, skewed distributions.

Your data may differ:
- **Higher selectivity** (filter matches 0.1% vs 40%) → database time changes, library overhead constant
- **Wider tables** (30 columns vs 8) → serialization cost increases, projection value rises
- **Deeper graphs** (5 levels vs 2) → nested query cost increases, FlexQuery advantage grows

Run a small pilot with your schema to validate assumptions.

---

## Conclusion

Benchmarks provide directional guidance, not absolute truth. Use them to:

1. **Rule out obviously slow approaches** (e.g., Sieve with complex filters in hot path)
2. **Validate architectural claims** (e.g., "eager parsing adds negligible overhead" — confirmed)
3. **Set expectations** (e.g., "dynamic projection adds ~20 µs" — true)
4. **Identify trade-offs** (e.g., "Gridify is faster but lacks projection" — documented)

Then **test with your own workload**. The benchmark suite is open-source and reproducible for that purpose.
