# FlexQuery.NET — Benchmark Suite

A professional, fair, and reproducible benchmarking suite comparing FlexQuery.NET against popular .NET query libraries.

---

## Philosophy

This suite prioritizes **engineering transparency** over marketing. Every benchmark includes honest documentation about what it measures and what it does NOT measure.

**We do NOT claim:**
- "FlexQuery.NET is X times faster than Y"
- "Library Z is slow"

**We DO measure:**
- Parsing overhead across different DSL complexities
- Expression tree generation cost vs handwritten LINQ
- End-to-end execution including materialization
- Memory allocations and GC pressure

### Architectural Differences (Important Context)

| Library | Architecture | Parsing | Expression Gen | What It Optimizes For |
|---|---|---|---|---|
| **FlexQuery.NET** | DSL → AST → Expression | Eager (full AST) | Runtime expression trees | Unified query abstraction + governance |
| **Gridify** | DSL → Lazy expression | Lazy (at execution) | Runtime expression trees | Simplicity + performance |
| **Sieve** | Attribute-driven | Attribute reflection | Delegate-based | Convention over configuration |
| **Dynamic.Core** | String → Expression | Combined with execution | Runtime expression trees | Maximum flexibility |
| **Handwritten LINQ** | Compile-time | N/A (JIT compiled) | Compile-time | Maximum performance (baseline) |

---

## Benchmark Categories

### 1. Parsing (`Benchmarks/Parsing/`)
Measures the CPU and allocation cost of converting query strings into internal models.
- `SimpleFilterParsingBenchmarks` — single equality filter
- `ComplexFilterParsingBenchmarks` — multi-field AND, nested any(), mixed operators, full parameter sets

### 2. Expression Generation (`Benchmarks/Expressions/`)
Measures the cost of building LINQ expression trees from pre-parsed models.
- `ExpressionGenerationBenchmarks` — simple eq, multi-field, nested collection predicates
- Compares: Handwritten LINQ (baseline), FlexQuery.NET, Dynamic.Core

### 3. End-to-End Execution (`Benchmarks/Execution/`)
Measures the full pipeline from parsed options through materialization using EF Core InMemory.
- `EndToEndQueryBenchmarks` — filter+sort+page, nested any(), projection
- Compares: Handwritten (baseline), FlexQuery.NET, Gridify, Dynamic.Core
- Dataset: 1,000 users, ~2,500 orders, ~5,000 items (deterministic seed)

---

## How to Run

```bash
# Interactive menu — choose which benchmark to run
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks/FlexQuery.Benchmarks.csproj

# Run specific categories
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks/FlexQuery.Benchmarks.csproj -- --filter *Parsing*
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks/FlexQuery.Benchmarks.csproj -- --filter *Expression*
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks/FlexQuery.Benchmarks.csproj -- --filter *EndToEnd*

# List all available benchmarks
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks/FlexQuery.Benchmarks.csproj -- --list flat
```

> **Important:** Always run in **Release** mode without a debugger attached. Debug mode results are meaningless.

---

## Project Structure

```
benchmarks/FlexQuery.Benchmarks/
├── Benchmarks/
│   ├── Parsing/
│   │   ├── SimpleFilterParsingBenchmarks.cs
│   │   └── ComplexFilterParsingBenchmarks.cs
│   ├── Expressions/
│   │   └── ExpressionGenerationBenchmarks.cs
│   └── Execution/
│       └── EndToEndQueryBenchmarks.cs
├── Infrastructure/
│   ├── Database/
│   │   └── BenchmarkDbContext.cs
│   └── Seed/
│       └── DataSeeder.cs
├── Models/
│   └── BenchmarkEntities.cs
├── Program.cs
├── FlexQuery.Benchmarks.csproj
└── README.md
```

---

## Dataset

All benchmarks use a **deterministic seed** (`Random(42)`) for reproducibility:

| Entity | Count | Notes |
|---|---|---|
| Users | 1,000 | Distributed across 8 cities, 3 statuses |
| Orders | ~2,500 | 0–5 per user, 4 status types |
| OrderItems | ~5,000 | 1–3 per order, linked to 50 products |
| Payments | ~2,500 | 0–2 per order, 4 payment methods |
| Products | 50 | 5 categories |

---

## Environment Requirements

- .NET 8.0 SDK
- Release mode execution (`-c Release`)
- No debugger attached
- For SQL benchmarks: local SQL Server instance (swap provider in `BenchmarkDbContextFactory`)

---

## Interpreting Results

BenchmarkDotNet reports:
- **Mean**: Average execution time
- **Allocated**: Heap allocations per operation
- **Gen0/Gen1/Gen2**: GC collection counts

When comparing:
- Handwritten LINQ is always the theoretical optimum — it has zero parsing/expression cost.
- Dynamic libraries trade runtime cost for flexibility. The question is whether the overhead is acceptable for your use case.
- Allocation differences matter most under high-throughput scenarios (thousands of requests/second).

For detailed interpretation guidance, see [docs/guide/performance/interpretation-guide.md](../../docs/guide/performance/interpretation-guide.md).

---

## Known Benchmark Gaps

Benchmarks are inherently incomplete. The following scenarios are **not** covered by this suite:

- **Concurrent load** — No multi-threaded request simulation (e.g., 1000 simultaneous queries)
- **Network latency** — In-process TestServer excludes TCP stack, TLS handshake
- **Serialization deep dive** — Only default `System.Text.Json`; custom converters, polymorphism not tested
- **Database variety** — SQL Server LocalDB + EF Core InMemory only; PostgreSQL, MySQL, Oracle not included
- **Caching layers** — No Redis, no EF Core query cache, no response cache
- **AOT compilation** — Native AOT (PublishAot) not measured
- **Cold start** — JIT compilation excluded by warmup iterations; serverless cold starts not measured
- **Long-running processes** — Memory leak detection, heap fragmentation over hours/days

These omissions are intentional. Each would require a separate benchmark harness with different tooling (e.g., `k6` for HTTP load, `dotnet-counters` for production profiling). They are candidates for future expansion.

---

## Re-running These Benchmarks

To verify results on your own hardware:

### 1. Prerequisites

```bash
# Verify .NET 8+ SDK
dotnet --version  # Should output 8.x.x
```

### 2. Clone and Build

```bash
git clone https://github.com/peterjohncasasola/FlexQuery.NET.git
cd FlexQuery.NET/benchmarks/FlexQuery.Benchmarks
dotnet restore
dotnet build -c Release
```

### 3. Run Benchmarks

```bash
# Run full suite (15–30 minutes)
dotnet run -c Release

# Run specific category
dotnet run -c Release -- --filter "*Parsing*"
dotnet run -c Release -- --filter "*Expression*"
dotnet run -c Release -- --filter "*EndToEnd*"
dotnet run -c Release -- --filter "*ApiEndToEnd*"

# List all benchmarks
dotnet run -c Release -- --list flat
```

> **Important:** Always run in `Release` mode without debugger attached. Debug mode results are meaningless.

---

## SQL Server Benchmarks

For database execution benchmarks (100K records), initialize LocalDB:

```bash
dotnet run -c Release -- setup
```

This creates and seeds `FlexQueryBenchmarks` database. It may take several minutes for 100K records. Delete with:

```bash
dotnet run -c Release -- --clean  # if implemented
```

Or manually: `DROP DATABASE FlexQueryBenchmarks;`

---

## Benchmark Artifacts

Results are written to `BenchmarkDotNet.Artifacts/` with timestamped subfolders:

```
BenchmarkDotNet.Artifacts/
├── results/
│   ├── FlexQuery.Benchmarks.Benchmarks.Parsing.SimpleFilterParsingBenchmarks-report-github.md
│   ├── FlexQuery.Benchmarks.Benchmarks.Parsing.SimpleFilterParsingBenchmarks-report.html
│   ├── FlexQuery.Benchmarks.Benchmarks.Parsing.SimpleFilterParsingBenchmarks-report.csv
│   └── ... (one set per benchmark class)
└── logs/
    └── BenchmarkRun-{timestamp}.log
```

- `*-github.md` — Markdown table for GitHub/README inclusion
- `*.html` — Interactive HTML with charts (open in browser)
- `*.csv` — Raw numeric data for custom analysis (Excel, Pandas)

---

## Contributing Benchmarks

If you believe a scenario is missing or incorrectly configured:

1. **Open an issue** describing the proposed benchmark
2. **Provide a code sample** showing how the library should be invoked (preferably using its official recommended API)
3. **Justify the dataset** size and shape — why this scenario matters
4. **Explain why existing benchmarks** do not cover the scenario

We prioritize **fair comparison** over **crowded comparison tables**. A single, well-documented benchmark is better than three poorly-configured ones.

