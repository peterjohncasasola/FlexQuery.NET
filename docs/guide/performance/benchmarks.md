# Performance Benchmarks

FlexQuery.NET's benchmark suite is designed with one principle: **transparency**.

We measure every stage of the query pipeline separately and publish the full methodology so you can make informed decisions about whether FlexQuery.NET is appropriate for your workload.

> **Important:** Benchmarks measure specific scenarios. Your actual performance depends on your data shape, database engine, network latency, caching, and serialization configuration. Always benchmark with your own production-like workload.

---

## Documentation Structure

1. **[Methodology](./methodology.md)** — Environment, hardware, dataset, reproducibility
2. **[Execution Benchmarks](./execution.md)** — Full query pipeline (filter, sort, page, projection, nested)
3. **[Parsing Performance](./parsing-performance.md)** — String → Abstract Syntax Tree (AST) conversion cost
4. **[Expression Generation](./expression-generation.md)** — AST → LINQ Expression translation
5. **[Database Execution](./database-execution.md)** — SQL Server LocalDB results
6. **[API Benchmarks](./api-benchmarks.md)** — Full ASP.NET Core pipeline vs OData/GraphQL/Gridify/Sieve
7. **[Scalability](./scalability.md)** — Performance across 100 to 10,000 records
8. **[Memory Usage](./memory-usage.md)** — Memory allocation patterns and GC pressure
9. **[Fairness Disclaimers](./fairness-disclaimers.md)** — Architectural differences, what's measured vs not
10. **[Interpretation Guide](./interpretation-guide.md)** — How to apply these numbers to your use case

---

## Quick-Start Summary

If you want the bottom line without reading all pages:

| Scenario | Expectation | Where to Look |
|:----------|:-------------|:--------------|
| **Simple filter + sort + page (20 items)** | FlexQuery matches or exceeds handwritten LINQ; 1–2 ms total latency per request | [Execution](./execution.md) |
| **Dynamic projection (select specific fields)** | FlexQuery adds ~20 µs overhead vs strongly-typed handwritten; allocation-efficient | [Execution: Projection](./execution.md#scenario-2-dynamic-projection-select-subset-of-fields) |
| **Nested collection queries (`Any`/`All`)** | Library overhead is negligible (< 2% of total time); database I/O dominates | [Execution: Nested](./execution.md#scenario-3-nested-collection-queries-any) |
| **Large result sets (10,000+ rows)** | Memory scales linearly; consider streaming or paging to prevent OOM | [Scalability](./scalability.md) |
| **SQL Server production** | FlexQuery adds ~5% overhead vs handwritten; still fastest among dynamic libraries | [Database Execution](./database-execution.md) |
| **Full API request (including serialization)** | FlexQuery 0.55–1.00× relative to baseline depending on page size | [API Benchmarks](./api-benchmarks.md) |

---

## Key Findings (Condensed)

1. **End-to-end execution** — FlexQuery.NET performs comparably to handwritten LINQ for common filter+sort+page scenarios (within 0–5%), while providing runtime flexibility and governance.

2. **Parsing vs execution trade-off** — Libraries with deferred parsing (Gridify, Sieve) show faster parsing micro-benchmarks but similar or slightly slower overall execution. FlexQuery's eager parsing front-loads work and enables validation caching, resulting in lower memory allocation.

3. **Memory allocation** — FlexQuery.NET maintains consistent allocation profiles (21–352 KB per request) across scaling factors, while other libraries vary more widely.

4. **Architectural differences matter** — OData and GraphQL include middleware layers, serialization formats, and protocol overhead that FlexQuery.NET does not have. These are not "slower" — they solve different problems with different feature sets.

5. **Nested queries** — For `Any`/`All` scenarios, the choice of dynamic query library has **no measurable impact** (< 2% difference) because database traversal dominates runtime.

---

## Benchmark Philosophy

We **do not** claim:
- FlexQuery.NET is the "fastest" library across all scenarios
- FlexQuery.NET always beats Gridify/Sieve/OData/GraphQL
- These numbers will match your production environment exactly

We **do** claim:
- Results are reproducible on any machine with the same hardware/configuration
- Architectural differences are documented and explained in [Fairness Disclaimers](./fairness-disclaimers.md)
- Libraries are configured using their recommended production patterns
- All benchmarks use deterministic datasets (`Random(42)`) for fairness

For the full philosophy, read the [benchmark suite README](../../benchmarks/FlexQuery.Benchmarks/README.md).

---

## Reproducing These Results

```bash
# Clone the repository
git clone https://github.com/peterjohncasasola/FlexQuery.NET.git
cd FlexQuery.NET/benchmarks/FlexQuery.Benchmarks

# Restore and build
dotnet restore
dotnet build -c Release

# Run all benchmarks (~20–30 minutes)
dotnet run -c Release

# Or run a specific category
dotnet run -c Release -- --filter "*Execution*"
dotnet run -c Release -- --filter "*ApiEndToEnd*"
```

Results are written to `BenchmarkDotNet.Artifacts/results/` as:
- `*-report-github.md` — Markdown tables for GitHub/README
- `*-report.html` — Interactive HTML with charts
- `*.csv` — Raw data for custom analysis

---

## What's Not Benchmarked

These scenarios are intentionally **out of scope** for the current suite:

- **Concurrent load** — Single-threaded; no load-testing with 1000+ simultaneous requests
- **Network latency** — In-process TestServer; no TCP stack, no TLS
- **Serialization deep dive** — Only default `System.Text.Json`; custom converters not tested
- **Database variety** — Only SQL Server LocalDB + EF Core InMemory
- **Caching strategies** — No Redis, no EF Core query cache, no response cache
- **AOT compilation** — Native AOT (dotnet publish -p:PublishAot=true) not measured

Each would require a separate benchmark harness (e.g., k6 for HTTP load, different DB providers). They are candidates for future expansion.

---

## How to Read Numbers

### Mean Time

Average execution time in **microseconds** (µs) or **milliseconds** (ms).
- 1 µs = 0.001 ms = 1,000 nanoseconds
- 50 µs = 0.05 ms = database query at scale
- 1.5 ms = typical API response for simple filter

### Allocated

Heap memory allocated per operation (per query). Lower is better for high-throughput scenarios.

| Allocated | Interpretation |
|:----------|:---------------|
| < 50 KB | Excellent — minimal GC pressure |
| 50–200 KB | Good — occasional Gen0 |
| 200–500 KB | Moderate — Gen0 every few requests |
| > 500 KB | Concern — Gen1+ possible under load |

### Gen0/Gen1/Gen2 Collections

Per 1,000 operations. High Gen1/Gen2 indicates allocation of medium/long-lived objects that survive collections.

- **Gen0 only:** Short-lived allocations, cheap to collect (< 100 µs)
- **Gen1 present:** Medium-lived, slightly more expensive
- **Gen2 present:** Long-lived heap fragmentation risk; stop-the-world collection (milliseconds)

See [Scalability](./scalability.md) for how these grow with result count.

---

## Getting Help

- **Benchmark code:** See `benchmarks/FlexQuery.Benchmarks/Benchmarks/` directory
- **Methodology questions:** See [Methodology](./methodology.md)
- **Results interpretation:** See [Interpretation Guide](./interpretation-guide.md)
- **Report a benchmark issue:** Open GitHub issue with reproduction steps
