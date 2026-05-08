# Expression Generation Benchmark

Expression generation benchmarks measure the CPU and allocation cost of translating an internal query model (parsed AST) into a `System.Linq.Expressions.Expression` tree.

This phase represents the core computational work performed by dynamic query libraries after parsing but before database execution.

> **Note:** This benchmark currently measures only FlexQuery.NET. Other libraries (Gridify, Sieve, System.Linq.Dynamic.Core) are not included in this specific benchmark class. Comparisons would require extending the benchmark suite.

---

## Current Results

**Scenario:** Generate a simple equality filter predicate: `x => x.Status == "active"`

This benchmark:
- Uses a pre-parsed `QueryOptions` AST
- Calls `ExpressionBuilder.BuildPredicate<User>()`
- Measures pure LINQ expression construction time
- Runs completely in-memory, no database involved

| Benchmark Method | Mean | Error | StdDev | Gen0 | Allocated |
|:-----------------|-----:|------:|-------:|-----:|----------:|
| FlexQuery_ExpressionGeneration | 915.8 ns | 10.36 ns | 9.69 ns | 0.2241 | 2.06 KB |

**Interpretation:**
- **~0.9 µs** to build a simple predicate expression tree from an AST.
- **2.06 KB** allocated per expression, primarily for `ParameterExpression`, `MemberExpression`, and `ConstantExpression` nodes.
- Gen0 rate of 0.22 per 1,000 operations indicates very short-lived allocations; these objects are collected quickly and do not contribute to Gen1/Gen2 pressure.

---

## Context: Why This Matters

Generating LINQ expressions dynamically is non-trivial because expression trees are immutable and must be constructed node-by-node. A poorly optimized generator can allocate dozens of temporary objects and perform excessive reflection.

FlexQuery.NET's design keeps allocation flat regardless of filter complexity (within the same expression generation call), thanks to:
- Cached `PropertyInfo` resolution
- Direct node construction without closure classes
- Iterative AST traversal (no deep recursion limits)

---

## What About Other Libraries?

The current benchmark category does not include Gridify, Sieve, or Dynamic.Core. To add them:
1. Identify the method that constructs the LINQ expression (or `IQueryable`).
2. Ensure the same pre-parsed model is used as input.
3. Measure the time from model to `IQueryable` (or to `Expression` if exposed).

This would provide a true apples-to-apples comparison of expression generation overhead across libraries. Contributions to extend the benchmark suite are welcome.

---

## Relationship to End-to-End Performance

Expression generation is one stage in the full pipeline:

```
Parse (0.1 µs) → Validate (1–10 µs) → Expression Generation (~0.9 µs) → Execute (500 µs – 50 ms)
```

At **~1 µs**, expression generation is negligible compared to database execution. Even for 10,000 QPS, the total CPU time is < 0.01 seconds per second — essentially free.

However, for **in-memory LINQ-to-Objects** scenarios (no database), expression generation cost becomes more visible (as seen in [Execution Benchmarks](./execution.md) where FlexQuery and handwritten are within milliseconds).

---

## Future Improvements

Potential additions for this benchmark category:
- Multi-field AND/OR expression generation
- Nested collection `Any` predicate generation
- Dynamic projection (`Select` into anonymous type)
- Caching impact: pre-compiled expression reuse
- Comparison to `System.Linq.Dynamic.Core` string-based compilation
