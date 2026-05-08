# Parsing Performance

Parsing performance benchmarks measure the raw cost of converting a query string into an internal representation. This reflects the CPU work and memory allocation required before any query can be executed.

> **⚠️ Architectural Context Required**
> Comparing parsing benchmarks directly between FlexQuery.NET and Gridify/Sieve is not an apples-to-apples comparison. See [Fairness Disclaimers](./fairness-disclaimers.md) for the full explanation of eager vs. deferred parsing architectures.

---

## Scenarios Covered

### Simple Filter Parsing

**Query:** `"Status:eq:Active"` (one equality condition)

Measures the minimum parsing work for a single field+operator+value triplet.

| Library | Mean | Allocated | Architecture |
|:---------|-----:|----------:|:-------------|
| **Gridify** | 3.53 ns | 40 B | Deferred — stores string only |
| **Sieve** | 3.78 ns | 48 B | Deferred — stores string only |
| **FlexQuery.NET** | 103.27 ns | 632 B | Eager — full AST construction |

**Analysis:** FlexQuery.NET performs ~29× more work here because it builds a normalized Abstract Syntax Tree (AST) including tokenization, operator normalization, and type inference. Gridify and Sieve merely instantiate a DTO; actual parsing is deferred until `Apply()` or `ApplyFiltering()` time.

For most production scenarios, this parsing overhead (0.1 µs) is negligible compared to database I/O (500 µs – 50 ms). However, it becomes relevant only when processing millions of lightweight queries per second with no database involved.

---

### Complex Filter Parsing

**Query:** `"status:eq:active,age:gt:25,city:eq:London"` (three-field AND)

Measures parsing cost with multiple conditions chained together.

| Library | Mean | Allocated | vs Simple |
|:---------|-----:|----------:|----------:|
| **Gridify** | 3.65 ns | 40 B | +3% |
| **Sieve** | 3.80 ns | 48 B | +1% |
| **FlexQuery.NET** | 117.47 ns | 632 B | +14% |

FlexQuery.NET's parsing time increases linearly with condition count but remains under 200 ns. Gridify and Sieve show negligible increase because they still only store the concatenated string.

---

### Mixed Operators

**Query:** `"name:contains:john,age:gte:21,status:in:active|pending"`

Measures parsing when operators differ (contains, greater-than-or-equal, multi-value IN).

| Library | Mean | Allocated |
|:---------|-----:|----------:|
| **Gridify** | 3.54 ns | 40 B |
| **Sieve** | 3.77 ns | 48 B |
| **FlexQuery.NET** | 132.18 ns | 632 B |

FlexQuery's operator complexity cost is minimal (~15 ns over multi-And). The additional time accounts for operator lookup tables and value tokenization (splitting `active|pending` into array).

---

### Nested Collection Predicates

**Query:** `"orders:any:status:eq:shipped"`

FlexQuery.NET-only capability. Gridify and Sieve do not support nested `any()` predicates out of the box.

| Library | Mean | Allocated |
|:---------|-----:|----------:|
| **FlexQuery.NET** | 106.47 ns | 632 B |

**Analysis:** Nested predicates require additional AST node traversal (building `Any` node with child filter group) but add negligible overhead (~10 ns over simple filter). This demonstrates that FlexQuery's eager parsing scales to moderately complex queries without penalty.

---

### Full Parameter Set

**Query:** Filter + Sort + Page + Select all together

Parsing the complete query string with all FlexQuery parameters enabled.

| Library | Mean | Allocated |
|:---------|-----:|----------:|
| **FlexQuery.NET** | 159.75 ns | 656 B |

Gridify and Sieve are excluded here because they do not parse sort/pagination/selection during object construction — those string values are stored verbatim and interpreted later during the `Apply` phase.

---

## DSL Format Comparison (FlexQuery.NET Only)

FlexQuery.NET supports two textual formats: **DSL** (colon-delimited) and **JQL** (SQL-like).

| Format | Example | Mean | Allocated | Relative |
|:-------|---------|-----:|----------:|---------:|
| **DSL** | `status:eq:active` | 313.9 ns | 784 B | 1.00× |
| **JQL** | `status = "active"` | 566.0 ns | 1,440 B | 1.80× |

JQL parsing is ~1.8× slower due to more complex tokenization (handling quotes, whitespace, operator precedence). This is expected and still sub-microsecond.

---

## Parsing is Not the Whole Story

These numbers can be misleading. A library that appears "fast" at parsing may defer all work to execution time, where costs multiply.

Consider the full pipeline for a typical API request:

```
Stage                    │ FlexQuery.NET │ Gridify │ Sieve
────────────────────────┼───────────────┼─────────┼─────────
Parse ( upfront )        │   0.1 µs      │  <0.01  │  <0.01
Validate ( field checks )│   5–50 µs     │   0 µs  │   0 µs
Expression generation    │   1–5 µs     │   0 µs  │   0 µs
EF Core translation      │  100–500 µs  │ 100–500 │ 100–500
Database execution       │  500 µs–50 ms│ same    │ same
JSON serialization       │  100–5000 µs │ same    │ same
────────────────────────┴───────────────┴─────────┴─────────
Total (typical)          │   1–55 ms    │ similar │ similar
```

FlexQuery.NET front-loads work (parse + validate) so that errors fail fast before database access. Gridify and Sieve defer parsing until `Apply()` is called inside the expression tree, which means errors surface during EF Core query translation — a more expensive failure mode.

---

## Should You Care About Parsing Benchmarks?

**Short answer:** Not in isolation. Parsing is one phase of a multi-stage pipeline.

Unless your workload consists of:
- **Millions of queries per second** on the same endpoint
- **No database involvement** (pure in-memory filtering of pre-loaded collections)
- **Tight latency budgets** where every microsecond matters

...then parsing overhead is unlikely to be your bottleneck.

**Focus on the [End-to-End Execution Benchmarks](../execution.md) instead.** Those reflect actual HTTP request latency.
