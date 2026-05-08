# Benchmark Methodology

Transparency is the foundation of our performance claims. This page details exactly how we measure FlexQuery.NET and how you can reproduce these results on your own hardware.

---

## Environment

All benchmarks are executed on the following infrastructure:
- **CPU:** 13th Gen Intel(R) Core(TM) i5-13500H (16 Cores), ~2.6GHz
- **RAM:** 24GB (24576MB) 5200MHz
- **OS:** Windows 11 Pro 64-bit (Build 26200)
- **GPU:** NVIDIA GeForce RTX 4050 Laptop GPU (6GB VRAM)
- **Runtime:** .NET 8.0.x
- **Database:** SQL Server 2022 (LocalDB)

---

## Dataset

We use a deterministic seeding process (`Random(42)`) to ensure every benchmark run uses the exact same data.
- **Records:** 100,000 Users
- **Graph Depth:** 3 levels (`User -> Order -> OrderItem`)
- **Scale:** ~250,000 Orders, ~750,000 OrderItems

---

## Measurement Tools

### BenchmarkDotNet
We use the industry-standard [BenchmarkDotNet](https://benchmarkdotnet.org/) library.
- **Warmup:** Minimum 3 iterations.
- **Target:** Minimum 10 iterations.
- **Memory:** `MemoryDiagnoser` is enabled for all runs to track allocations and GC pressure.

### Database Benchmarking
For SQL Server benchmarks:
- We use `AsNoTracking()` to avoid EF Core cache pollution.
- We force materialization using `ToListAsync()` or `ToList()`.
- We use a local SQL Server instance to minimize network variance while still measuring real protocol overhead.

---

## Fair Comparison Policy

### FlexQuery.NET (Eager Parsing)
FlexQuery.NET performs extensive validation and AST normalization upfront. While this makes its "parsing" micro-benchmarks slower than deferred-execution libraries, it results in a more robust and governable pipeline.

### Gridify & Sieve (Deferred Parsing)
We configure these libraries using their recommended production settings. We measure them at the "Apply" phase where they perform their actual work to ensure a fair end-to-end comparison.

### OData & GraphQL
These are measured via a real ASP.NET Core `TestServer`. This includes the overhead of HTTP routing, middleware, and serialization, which is the only way to fairly compare such different architectural patterns.

---

## Reproducing Results

To run the benchmarks yourself:

1. **Clone the repository:**
   ```bash
   git clone https://github.com/peterjohncasasola/FlexQuery.NET.git
   ```

2. **Setup the Database:**
   ```bash
   cd benchmarks/FlexQuery.Benchmarks
   dotnet run -c Release -- setup
   ```

3. **Execute Benchmarks:**
   ```bash
   dotnet run -c Release -- --filter *Execution*
   ```

4. **Generate Charts (requires Python + Pandas/Matplotlib):**
   ```bash
   python ../generate_charts.py
   ```
