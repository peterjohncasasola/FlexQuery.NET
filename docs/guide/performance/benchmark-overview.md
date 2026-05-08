# Performance Benchmarks Overview

FlexQuery.NET is built for high-performance, production-grade APIs. To ensure transparency and prevent regressions, we maintain a comprehensive, open-source benchmarking suite using [BenchmarkDotNet](https://benchmarkdotnet.org/).

Unlike many libraries that publish single, cherry-picked numbers, we break down the query pipeline into discrete phases. This allows you to accurately assess the cost of parsing, expression generation, and database execution.

---

## Architectural Context: Eager vs. Deferred Parsing

When evaluating performance in the dynamic LINQ ecosystem, it is critical to understand the difference between **Eager** and **Deferred** parsing architectures.

### Eager Parsing (FlexQuery.NET)
FlexQuery.NET parses the raw string query into a normalized, strongly-typed Abstract Syntax Tree (AST) upfront. 
- **Pros:** Fail-fast validation, easy to manipulate the AST before execution, allows deep governance (e.g., operator restrictions, field-level security), and enables AST caching.
- **Cons:** Higher initial parsing cost.

### Deferred/Lazy Parsing (Gridify, Sieve)
Libraries like Gridify and Sieve delay the heavy lifting until the query is actually executed (`Apply` or `ApplyFiltering`).
- **Pros:** "Parsing" benchmarks look exceptionally fast because almost no work is actually done at parse time.
- **Cons:** Errors are caught later in the pipeline, making robust pre-execution governance difficult.

Because of this architectural difference, **comparing pure parsing benchmarks between FlexQuery.NET and deferred-execution libraries is not an apples-to-apples comparison.** 

---

## Benchmark Suite Structure

We separate our benchmarks into three distinct categories to provide maximum transparency:

1. **[Parsing Performance](./parsing-performance.md)**
   Measures the raw cost of converting a string into an internal representation.

2. **[Expression Generation Benchmarks](./expression-generation.md)**
   Measures the CPU cost and memory allocation of translating the internal model into a concrete `System.Linq.Expressions.Expression` tree.

  3. **[End-to-End Execution Benchmarks](../execution.md)**
   Measures the full lifecycle from query string to materialized results using the EF Core InMemory provider.

4. **[API Benchmarks](./api-benchmarks.md)**
   Real-world ASP.NET Core request/response performance comparing FlexQuery.NET against OData, GraphQL, Gridify, and Sieve. Includes scaling analysis from 20 to 50,000 records.

5. **[Database Execution Benchmarks](./database-execution.md)**
   Measures raw SQL translation and execution performance against SQL Server LocalDB with 100,000+ records.

---

## Benchmark Transparency & Honesty

We commit to the following principles in our benchmarks:

1. **No Client-Side Evaluation:** All queries are executed entirely server-side (translated to SQL or EF Core InMemory). We ensure no `IEnumerable` fallback occurs.
2. **Materialized Results:** End-to-end benchmarks force materialization (`ToList()` or equivalent) to prevent deferred execution from hiding performance costs.
3. **Deterministic Datasets:** We use realistic, multi-entity datasets seeded with deterministic values (`Random(42)`) to ensure fair pagination and filtering comparisons.
4. **Apples-to-Apples Comparisons:** When comparing against other libraries, we configure them to use the most optimal, production-ready settings available for that library.

---

## Navigating the Results

If you are a library consumer deciding whether to adopt FlexQuery.NET, we recommend focusing entirely on the **[End-to-End Execution Benchmarks](../execution.md)**. This reflects the actual latency added to your HTTP requests.

If you are a contributor or interested in compiler design, the **[Parsing Performance](./parsing-performance.md)** and **[Expression Generation](./expression-generation.md)** benchmarks provide deep insights into our pipeline optimizations.
