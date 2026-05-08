# Performance Benchmark Documentation

FlexQuery.NET's benchmark suite is designed with one principle: **transparency**.

We measure every stage of the query pipeline separately and publish the full methodology so you can make informed decisions about whether FlexQuery.NET is appropriate for your workload.

> **Important:** Benchmarks measure specific scenarios. Your actual performance depends on your data shape, database engine, network latency, caching, and serialization configuration. Always benchmark with your own production-like workload.

---

## Documentation Structure

1. **[Methodology](./methodology.md)** — Environment, hardware, dataset, reproducibility
2. **[Parsing Performance](./parsing-performance.md)** — String → Abstract Syntax Tree (AST) conversion cost
3. **[Expression Generation](./expression-generation.md)** — AST → LINQ Expression translation
4. **[Execution Benchmarks](./execution.md)** — Full query pipeline (filter, sort, page, projection, nested)
5. **[Database Execution](./database-execution.md)** — SQL Server LocalDB results
6. **[API Benchmarks](./api-benchmarks.md)** — Full ASP.NET Core pipeline vs OData/GraphQL/Gridify/Sieve
7. **[Scalability](./scalability.md)** — Performance across 100 to 10,000 records
8. **[Fairness Disclaimers](./fairness-disclaimers.md)** — Architectural differences, what's measured vs not
9. **[Interpretation Guide](./interpretation-guide.md)** — How to apply these numbers to your use case

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

5. **Nested queries** — For `Any`/`All` scenarios, the choice of dynamic library has **no measurable impact** (< 2% difference) because database traversal dominates runtime.

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

## Benchmark Documentation Best Practices

When contributing benchmark results or updating this documentation, follow these guidelines to maintain credibility, clarity, and maintainability.

### Markdown Tables vs. Screenshots/Charts

**Prefer markdown tables** for:
- Exact numeric comparisons (mean, error, allocated)
- Copy-pasteable data for readers to analyze
- GitHub rendering (tables are native and accessible)

**Use screenshots or charts** for:
- Trending over time (multiple benchmark runs across versions)
- Scaling curves (dataset size vs latency)
- Memory allocation heatmaps
- Visual callouts in blog posts or presentations

Chart generation: BenchmarkDotNet can export `*.csv`; use Python + Pandas/Matplotlib or Excel to create line/bar charts. Store charts in `docs/guide/performance/images/` and reference them with relative paths.

### Presenting Artifact Results Professionally

1. **Include the header** — Always copy the BenchmarkDotNet environment header (runtime, CPU, OS) from the artifact to provide context.
2. **Show Mean ± Error** — Never report just Mean; include Error (99.9% CI) to indicate statistical significance.
3. **Report Allocated** — Memory allocation is as important as latency for high-throughput systems.
4. **Mark baseline clearly** — Use `**bold**` or `(baseline)` indicator.
5. **Link to full artifact** — Provide a note: "Full results available in `BenchmarkDotNet.Artifacts/results/ClassName-report.html`"
6. **Do not cherry-pick** — Show all competing libraries in the same table, not separate tables each claiming "fastest".

### GitHub-Friendly Formatting

- Use **GitHub-flavored markdown** tables (as generated by `MarkdownExporter.GitHub`).
- Keep column count reasonable: 5–7 columns max before wrapping occurs.
- Align numeric columns to the right (`---:`).
- Use relative ratios where meaningful (1.00× baseline, <1.00× faster, >1.00× slower).
- Avoid excessive decimal places: µs → 1 decimal if >10, integer if <10. Memory → 1 decimal for KB, integer for bytes.

Example:

| Library | Mean | Error | Allocated | Relative |
|:---------|-----:|------:|----------:|---------:|
| **FlexQuery.NET** | 17.67 ms | 0.13 ms | 21.42 KB | 1.00× |
| Handwritten LINQ | 40.21 ms | 0.22 ms | 97.11 KB | 2.28× |

### Docs Site Layout

Organize performance docs hierarchically:

```
/guide/performance/
├── index.md               ← Overview + quick-reference tables
├── methodology.md         ← Environment + reproducibility
├── parsing-performance.md   ← Per-benchmark deep-dive
├── expression-generation.md
├── execution.md
├── database-execution.md
├── api-benchmarks.md
├── scalability.md
├── fairness-disclaimers.md
├── interpretation-guide.md
└── images/                ← Charts (PNG, SVG)
    ├── api-scaling.png
    └── allocation-by-page.png
```

- **index.md** serves as the landing page with summary tables.
- **Per-category pages** provide narrative, full result tables, analysis.
- **Images** supplement but never replace numeric tables.

### What Belongs in Main README vs Separate Docs

**Main README (`README.md`):**
- High-level summary: 2–3 benchmark tables with most representative scenarios
- Links to detailed documentation
- No lengthy analysis, caveats can be brief (1–2 lines)

**Separate docs (`docs/guide/performance/`):**
- Full result tables for every benchmark class
- Methodology, dataset description, environment details
- Fairness disclaimers, interpretation guidance
- Per-scenario analysis and architectural notes
- Changelog of benchmark results across versions (optional)

This keeps the main README concise while providing deep-dive resources.

### Maintaining Benchmark Documentation

1. **Update artifact numbers automatically** — Write a script that parses `BenchmarkDotNet.Artifacts/results/*-report-github.md` and injects tables into markdown files via templating. This prevents manual transcription errors.
2. **Version the benchmark suite** — Tag benchmark runs with version numbers (e.g., `v2.4.0-benchmarks`) and keep historical results accessible.
3. **Document changes in methodology** — If you change dataset size, provider, or configuration, note it in a changelog section at the top of affected pages.
4. **Review with every release** — Include benchmark documentation updates in the release checklist.

### Reproducibility Checklist

Every benchmark page should include (or link to) the following information:
- [ ] Hardware specs (CPU, RAM, OS, .NET SDK version)
- [ ] Dataset size and distribution
- [ ] Seed value (e.g., `Random(42)`)
- [ ] Provider (InMemory, SQL Server, PostgreSQL, etc.)
- [ ] Configuration flags (e.g., `IncludeCount`, `AsNoTracking`, `MaxPageSize`)
- [ ] BenchmarkDotNet version
- [ ] Library versions being compared
- [ ] Complete source code listing or link to GitHub

If any are missing, the benchmark is not reproducible.

### Badges and Charts

**Do NOT use badges for relative performance** (e.g., "Fastest!" badge). These are subjective and can become outdated. If you must use badges:
- Use static shields for **specific measurable claims** (e.g., "Benchmarks: 100K+ comparisons" or "Performance data available")
- Do not claim superiority; instead link to the results.

**Charts in docs:**
- Line charts for scaling (dataset size vs mean latency)
- Bar charts for cross-library comparison (grouped by scenario)
- Include error bars where possible (or note that error is small and omitted for clarity)
- Use accessible colors (colorblind-friendly palette)
- Provide data table in caption or alt text.

### How Much Data in Main README?

Include **only the most common scenarios** in the main README's performance table:
- A simple filter+sort+page (most users' use case)
- Possibly SQL Server vs InMemory distinction (two small tables)
- Do not include all 20+ benchmark variations.

Keep the README table to **≤ 5 libraries × ≤ 3 scenarios** (15 data points max). Everything else belongs in separate pages.

### Moving Content to Separate Pages

Move to separate docs when:
- Explanation exceeds 3–4 paragraphs
- Needs table of contents or multiple subheadings
- Contains more than one benchmark result table
- Covers methodology, fairness, or interpretation

Keep in README:
- One-sentence summaries
- Links to detailed pages
- A single representative table

This prevents the README from becoming a wall of numbers.

---

## Getting Help

- **Benchmark code:** See `benchmarks/FlexQuery.Benchmarks/Benchmarks/` directory
- **Methodology questions:** See [Methodology](./methodology.md)
- **Results interpretation:** See [Interpretation Guide](./interpretation-guide.md)
- **Report a benchmark issue:** Open GitHub issue with reproduction steps
