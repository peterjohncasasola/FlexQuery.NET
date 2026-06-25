# FlexQuery.NET Roadmap

> **Status**: v3.0.x — Post-audit hardening phase  
> **Build**: ✅ 773+ tests passing  
> **Audit coverage**: Security ✅ | Governance ✅ | Architecture ✅ | Performance ✅ | Grouping ✅

---

## Recent Delivery

The following were implemented in this session:

| Item | Status | Effort | Risk |
|------|--------|--------|------|
| DynamicTypeBuilder bounded cache + stable key | ✅ Done | Low | None |
| DynamicTypeBuilder regression tests (9) | ✅ Done | Low | None |
| `InternalsVisibleTo` for FlexQuery.Benchmarks | ✅ Done | Trivial | None |
| Benchmarks: Validation (10/100/1000 fields) | ✅ Created | Low | None |
| Benchmarks: Projection (flat/nested/wildcard) | ✅ Created | Low | None |
| Benchmarks: Dapper SQL generation (simple/complex/aggregate) | ✅ Created | Low | None |
| Phase 3: Governance audit document | ✅ Done (.kilo/audit/phase3-governance-audit.md) | High | N/A |
| Phase 4: Performance audit document | ✅ Done (.kilo/audit/phase4-performance-audit.md) | High | N/A |

## Priority-Ranked Implementation Roadmap

### Priority 1: Ship Now

| # | Item | Effort | Impact | Risk | Why Now |
|---|------|--------|--------|------|---------|
| 1 | DynamicTypeBuilder bounded cache | **Low** | **High** | Low | Prevents unbounded dictionary growth; stable cache key |
| 2 | Enable caching by default (`EnableCache=true`) | **Trivial** | **High** | Low | All expression trees rebuilt per request; 1-line config change |
| 3 | Fix `DapperQueryOptions.IncludeTotalCount` default to match behavior | **Trivial** | **Medium** | Low | `totalCount = items.Count` is misleading when disabled |
| 4 | Fix `ExtractCountSql` keyword collision | **Low** | **Low** | Low | ORDER BY in subqueries |

### Priority 2: Investigate + Benchmark First

| # | Item | Effort | Impact | Risk | Why Not Yet |
|---|------|--------|--------|------|-------------|
| 5 | DapperRowHydrator compiled delegates | **Medium** | **High** | Medium | Internal API; need benchmarks to prove benefit at scale |
| 6 | ReflectionCache for property metadata | **Low** | **High** | Low | Pervasive uncached `GetProperty`/`GetProperties` calls |
| 7 | `SelectTreeBuilder.Build()` called 2× per request | **Trivial** | **Medium** | None | Store result on `options.Items` |
| 8 | `FilterNormalizer.Normalize` called redundantly | **Low** | **Medium** | Low | Cache normalized result |
| 9 | `BoundedConcurrentCache.Trim()` O(max) per insert | **Low** | **Medium** | Low | Batch eviction |

### Priority 3: Developer Experience

| # | Item | Effort | Impact | Why Not Yet |
|---|------|--------|--------|-------------|
| 10 | Add `InternalsVisibleTo` for benchmark project | **Trivial** | **Medium** | Enables running benchmarks against internal APIs |
| 11 | Fix pre-existing `DslParsingBenchmarks` references | **Low** | **Low** | Old benchmark references removed/refactored classes |
| 12 | Create sample ASP.NET minimal API project | **Medium** | **Medium** | Accelerates developer adoption |

### Priority 4: Deferred (Backlog)

| # | Item | Effort | Impact | Rationale |
|---|------|--------|--------|-----------|
| 13 | Expression tree balancing for deep filters | **Medium** | Low | Edge case; 1000+ conditions is rare |
| 14 | Wildcard expansion cache | **Low** | Low | Same patterns repeated per request, but small cost |
| 15 | DapperRowHydrator compiled delegates | **Medium** | High | Deferred until benchmarks prove 10K+ row cost |
| 16 | Collectible AssemblyBuilder (RunAndCollect) | **Low** | Low | DynamicTypeBuilder bounded cache sufficient |

---

## Implementation Plan: Priority 1

### 1. DynamicTypeBuilder — ✅ DONE

**Files changed**:
- `src/FlexQuery.NET/Helpers/DynamicTypeBuilder.cs` — Added bounded FIFO eviction, stable cache key, `Clear()`, `Count`
- `tests/FlexQuery.NET.Tests/Tests/DynamicTypeBuilderTests.cs` — 9 regression tests

**Verification**: 9/9 tests pass, core project builds clean.

### 2. Enable Caching by Default

**Change**: Set `FlexQueryCacheSettings.EnableCache = true` in static constructor.  
**Risk**: Cache keys must be stable across all query shapes. The `QueryCacheKeyBuilder.Build()` method already produces deterministic keys.  
**Benefit**: All expression trees (predicates, projections) cached after first hit.  
**Recommendation**: Change in v3.1.0 with clear release note.

### 3. Fix IncludeTotalCount Default

**Change**: In `FlexQueryDapperExtensions.ExecuteQueryAsync`, when `IncludeTotalCount` is false, set `totalCount = null` instead of `items.Count`.  
**Files**: `src/FlexQuery.NET.Dapper/FlexQueryDapperExtensions.cs`  
**Impact**: Consumers get accurate `TotalCount` semantics.

### 4. Fix ExtractCountSql

**Change**: Use word-boundary regex instead of `IndexOf` to detect ORDER BY/LIMIT/OFFSET keywords.  
**Files**: `src/FlexQuery.NET.Dapper/FlexQueryDapperExtensions.cs`

---

## Benchmark Execution

```bash
# Validation benchmarks
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks -- --filter *Validation*

# Projection benchmarks
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks -- --filter *Projection*

# Dapper SQL generation benchmarks
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks -- --filter *Dapper*

# All benchmarks
dotnet run -c Release --project benchmarks/FlexQuery.Benchmarks
```

---

## Audit Artifacts

| File | Content |
|------|---------|
| `.kilo/audit/phase3-governance-audit.md` | Full governance, security, provider consistency audit |
| `.kilo/audit/phase4-performance-audit.md` | Performance, memory, reflection, hardening audit |
| `.kilo/plans/1782230222805-eager-panda.md` | Phase 3 implementation plan |

---

## Known Issues

### Pre-existing (not from this session)

1. `DslParsingBenchmarks.cs` references `DslParser` and `JqlParser` classes that no longer exist in those namespaces — needs project reference cleanup
2. `Microsoft.Extensions.Caching.Memory` 6.0.0/8.0.0 has a known high-severity vulnerability (GHSA-qj66-m88j-hmgj) — should update to patched version
3. `System.Linq.Dynamic.Core` 1.3.8 has a known high-severity vulnerability (GHSA-4cv2-4hjh-77rx) — should update
4. Extensive CS1591 XML doc warnings — project should either document or suppress

### From this session

5. `DapperRowHydrator` is internal; benchmark project cannot test it without `InternalsVisibleTo` on `FlexQuery.NET.Dapper`

---

## Go-To-Market Checklist

- [x] Governance & security audited
- [x] Performance profiled
- [x] DynamicTypeBuilder memory leak fixed
- [x] 773+ existing tests passing
- [x] 9 new DynamicTypeBuilder tests
- [ ] Caching enabled by default (v3.1.0)
- [ ] NuGet vulnerability updates (CVE packages)
- [ ] Public benchmark results published
- [ ] Sample applications (EF Core + Dapper + AgGrid)
