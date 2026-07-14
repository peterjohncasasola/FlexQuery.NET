# Fairness Disclaimers and Architectural Differences

Benchmarking compares different solutions solving similar problems.

Before comparing raw numbers, it is critical to verify that the benchmarks are actually measuring equivalent work.

This document explains:

- what each benchmark category measures
- what it intentionally excludes
- where architectural differences affect interpretation
- why some comparisons can be misleading without context

The goal is transparency, not benchmark marketing.

---

# Benchmark Philosophy

Benchmarks should not be interpreted as universal rankings.

Different libraries optimize for different concerns:

- raw throughput
- developer ergonomics
- protocol compliance
- query governance
- runtime flexibility
- dynamic projection
- validation
- schema composition

A “faster” benchmark does not automatically mean a “better” architecture.

Benchmark results must always be interpreted alongside:
- feature requirements
- operational constraints
- maintainability goals
- API governance needs

---

# 1. Parsing Benchmarks: Eager vs Deferred Architectures

## Core Architectural Difference

| Aspect | FlexQuery.NET | Gridify | Sieve |
|:--|:--|:--|:--|
| Parsing Strategy | Eager AST generation | Deferred parsing | Deferred parsing |
| Parse() Behavior | Full lexer + AST build | DTO creation only | DTO creation only |
| Actual Work Happens | During `Parse()` | During `ApplyFiltering()` | During `Apply()` |
| AST Reusability | Yes | No | No |
| Cache-Friendly | Yes | Limited | Limited |

---

## Why Direct Parse Comparisons Are Misleading

FlexQuery.NET parsing benchmarks include:

- tokenization
- normalization
- AST generation
- operator validation
- property resolution
- type inference

Gridify and Sieve parsing benchmarks primarily measure:

- DTO allocation
- minimal object construction

Their actual query processing occurs later.

This means:

```text
FlexQuery Parse() benchmark
≠
Gridify/Sieve Parse() benchmark
```

They are measuring fundamentally different workloads.

---

## Example

A benchmark may show:

| Library | Parse Time |
|:--|--:|
| FlexQuery.NET | 103 ns |
| Gridify | 3.5 ns |

This appears to imply:
> Gridify is ~30× faster

However, Gridify still performs:
- string tokenization
- expression generation
- property resolution

later during execution.

When measuring end-to-end execution:
- the gap narrows significantly
- FlexQuery.NET may outperform deferred approaches due to AST reuse and caching

---

## Key Takeaway

Never compare parsing benchmarks across eager and deferred architectures without also measuring full execution pipelines.

---

# 2. Expression Generation Visibility

## FlexQuery.NET

FlexQuery.NET exposes expression generation as an explicit phase:

```csharp
ExpressionBuilder.BuildPredicate()
```

This allows:
- isolated benchmarking
- AST inspection
- caching strategies
- debugging visibility

---

## Gridify and Sieve

Gridify and Sieve combine:
1. parsing
2. expression generation
3. IQueryable application

inside a single call:

```csharp
ApplyFiltering()
Apply()
```

They do not expose internal expression generation separately.

---

## Why This Matters

Attempting to benchmark “only expression generation” for Gridify or Sieve would require:
- reflection
- internal API access
- artificial benchmark separation

This would not represent real-world usage.

For this reason:
- FlexQuery.NET exposes expression benchmarks
- Gridify/Sieve comparisons focus on end-to-end execution instead

---

# 3. Database vs InMemory Benchmarks

## InMemory Provider

### Advantages

✅ Fast  
✅ Isolated  
✅ Reproducible  
✅ No infrastructure dependency  

### Limitations

❌ No real SQL generation  
❌ No index usage  
❌ Different execution semantics  
❌ Different scanning behavior  

---

## SQL Server LocalDB

### Advantages

✅ Real SQL translation  
✅ Query plan generation  
✅ Real relational behavior  
✅ Production-like execution  

### Limitations

❌ Environment-dependent  
❌ Hardware-sensitive  
❌ More setup complexity  
❌ Higher variance between runs  

---

## Important Warning

Never use InMemory benchmarks alone to predict SQL Server production performance.

Relative ordering may change significantly.

Example:

| Library | InMemory Ratio | SQL Server Ratio |
|:--|--:|--:|
| FlexQuery.NET | 0.44× | 1.04× |
| Gridify | 1.00× | 1.28× |
| Sieve | 1.03× | 1.73× |

---

## Why Ordering Changes

InMemory execution:
- heavily favors expression shape
- avoids SQL translation cost
- avoids relational query planning

SQL Server execution:
- exposes database roundtrip overhead
- exposes SQL translation cost
- exposes query plan generation

---

# 4. API Benchmarks: Middleware vs Query Library

## OData

OData performs substantially more work than simple filter libraries.

OData handles:
- `$filter`
- `$select`
- `$expand`
- `$orderby`
- `$top`
- `$skip`
- `$count`
- EDM validation
- entity graph traversal

It also wraps responses with:
- `@odata.context`
- `@odata.count`

Comparing OData directly to lightweight filter libraries is therefore not feature-equivalent.

---

## GraphQL

GraphQL is a fundamentally different API paradigm.

GraphQL includes:
- schema parsing
- resolver orchestration
- query document ASTs
- nested field execution
- resolver pipelines

Performance depends heavily on:
- resolver implementation
- batching strategy
- DataLoader usage
- pagination configuration

A poorly-designed GraphQL resolver can be dramatically slower than REST.

The opposite can also be true.

---

## Manual LINQ Baseline

The “manual LINQ” baseline represents:
- hardcoded query logic
- fixed filters
- zero runtime flexibility

Example:

```csharp
_context.Users
    .Where(u => u.Status == "active")
```

This is useful as:
- a reference point
- a theoretical lower-overhead baseline

However, it is not feature-equivalent to dynamic query systems.

Manual LINQ does not provide:
- dynamic filtering
- operator governance
- projection parsing
- runtime query composition
- field-level validation

---

# 5. Allocation Measurements

BenchmarkDotNet memory measurements include:

✅ Managed heap allocations  
✅ Gen0/1/2 collections  
✅ In-process serializer allocations  

---

## What Is NOT Included

❌ Native memory  
❌ Database connection pools  
❌ OS page cache  
❌ TCP buffers  
❌ JIT warmup time  

---

# 6. Feature Parity Gaps

## Dynamic Projection

| Library | Dynamic Projection |
|:--|:--|
| FlexQuery.NET | ✅ |
| OData | ✅ |
| GraphQL | ✅ |
| Dynamic LINQ | ✅ |
| Gridify | ❌ |
| Sieve | ❌ |

Comparing projection benchmarks against libraries without projection support is not feature-equivalent.

---

## Nested Collection Predicates

| Library | Nested Any/All |
|:--|:--|
| FlexQuery.NET | ✅ |
| OData | ✅ |
| GraphQL | ✅ |
| Gridify | ❌ |
| Sieve | ❌ |

Example:

```text
orders:any(status:eq:shipped)
```

Libraries without nested collection support are excluded from those comparisons.

---

## Field-Level Governance

Only FlexQuery.NET and OData provide comprehensive query governance features such as:

- allowed field lists
- blocked fields
- operator restrictions
- depth limits
- validation pipelines

These features add small overhead but improve:
- API safety
- multi-tenant governance
- predictable execution behavior

---

# 7. What Is NOT Benchmarked

## Concurrent Load

Current benchmarks are single-threaded.

They do not measure:
- thread contention
- connection pool exhaustion
- CPU scheduling
- high-concurrency scenarios

Separate load-testing tools are required for this:
- k6
- Locust
- JMeter

---

## Network Latency

`TestServer` benchmarks run entirely in-process.

Real-world APIs add:
- TCP overhead
- TLS negotiation
- cloud latency
- proxy layers

Real network overhead often exceeds query parsing cost entirely.

---

## Serialization Variants

Benchmarks currently use:
```text
System.Text.Json
```

They do not measure:
- custom converters
- source generators
- Newtonsoft.Json
- polymorphic serialization

Serialization behavior may significantly affect overall request cost.

---

## Caching Layers

Benchmarks intentionally exclude:
- response caching
- CDN caching
- Redis
- query plan caches
- distributed caches

Caching can dramatically change relative overhead characteristics.

---

# 8. AST Caching Advantages

FlexQuery.NET's eager AST model enables:
- AST reuse
- compiled expression reuse
- validation metadata caching

Deferred DTO architectures cannot cache equivalent fully-processed AST structures as effectively.

This becomes especially beneficial in:
- dashboards
- grids
- repeated query patterns
- API gateways

---

# 9. Benchmark Configuration Transparency

All libraries are benchmarked using publicly documented production patterns.

We intentionally avoid:
- private APIs
- internal hacks
- unrealistic optimizations
- debug-only shortcuts

The goal is representative production-style usage.

---

# 10. Version Pinning

Benchmark results are version-sensitive.

Even small library updates may shift performance characteristics significantly.

All benchmark runs should explicitly document:
- package versions
- runtime versions
- benchmark framework versions

---

# 11. Environmental Assumptions

Benchmarks assume:
- warm database caches
- dedicated machine
- no external contention
- Server GC enabled
- no AOT compilation

Production systems may behave differently depending on:
- cloud infrastructure
- noisy neighbors
- network topology
- concurrent load
- database indexing

Always validate with your own workloads.

---

# 12. Real-World Perspective

In production systems:
- network latency frequently dominates
- database execution dominates
- serialization dominates at scale

Microsecond-level parsing differences rarely determine overall API responsiveness.

Maintainability, governance, flexibility, and operational safety often provide greater long-term value than minimal parsing overhead reductions.

---

# 13. Benchmark Reproducibility

Benchmark source code should remain:
- open
- reproducible
- transparent

Recommended benchmark repository structure:

```text
/benchmarks
    /Parsing
    /Execution
    /Api
    /Database
```

Include:
- hardware specs
- runtime versions
- benchmark configuration
- dataset generation scripts

---

# Conclusion

Benchmarks are tools for informed engineering decisions — not proof of universal superiority.

This document exists to clarify:
- what is being measured
- what is intentionally excluded
- where architectural differences affect interpretation
- when comparisons are fair

When evaluating query libraries, consider:
- governance requirements
- maintainability
- feature parity
- operational constraints
- production workloads

—not benchmark numbers alone.
