# Grouped Query Contract

**Last updated:** 2026-06-23

**Applies to:** FlexQuery.NET Core, EF Core provider, Dapper provider

---

## Purpose

This document defines the expected behavior of FlexQuery.NET when a query specifies
GroupBy + Aggregates + Sort + Paging. The result shape changes from the original
entity to a dynamic projection containing only group-key fields and aggregate aliases,
which fundamentally changes how sort and paging should behave.

---

## Result Shape

When GroupBy is specified, the query result is a **dynamic projection** containing:

1. **Group key fields** — one property per field in GroupBy
2. **Aggregate aliases** — one property per entry in Aggregates, keyed by Alias

The original entity's scalar properties are **not** present in the result shape.

### Example

```csharp
GroupBy = ["Category"]
Aggregates = [AVG(Price) AS priceAvg]
```

Projected shape:

```json
{ Category, priceAvg }
```

---

## Sort Rules

### Valid sort fields

In a grouped context, a sort field is valid only if it appears in the projected shape:

| Sort Field | Validity | Resolution |
|---|---|---|
| Group key field (e.g. Category) | ✅ Valid | Sorts by the group key value |
| Aggregate alias (e.g. priceAvg) | ✅ Valid | Sorts by the computed aggregate value |
| Aggregate source field (e.g. Price) | ✅ Resolved to alias | Mapped to the aggregate alias (priceAvg) |
| Entity field not in projection (e.g. Id) | ❌ Invalid | Silently removed |

### When all sorts are invalid

When every sort entry is invalid and no valid sort remains:

- A **fallback sort** is injected: ascending by the **first group-key field**
- This ensures paging remains deterministic

### Why silent removal (not rejection)?

- GroupBy + Aggregates are often specified separately from Sort (e.g., via an adapter
  like AG Grid). Rejecting the query at the FlexQuery level would be a breaking change for
  consumers that build the request in multiple steps.
- Silent removal matches the existing EF Core provider behavior and aligns with the
  tolerance principle: the system does the best it can with available information.

---

## Paging Rules

Paging in a grouped context operates on the **grouped result set** (groups + aggregates),
not on the source rows.

- If Sort is empty: a default sort by the first group-key field (ascending) is injected.
- If all sorts are invalid: the fallback rule above applies.
- Paging without a valid ORDER BY is **nondeterministic** and must be prevented.

---

## Provider Contract

### EF Core provider (FlexQuery.NET.EntityFrameworkCore)

| Scenario | Behavior |
|---|---|
| Sort by group key | ✅ Resolves correctly |
| Sort by aggregate alias | ✅ Resolves correctly |
| Sort by aggregate source field | ✅ Resolves to aggregate alias |
| Sort by non-projected field | ⚠️ Silently removed |
| All sorts invalid | ✅ Fallback to first group key ascending |
| Paging without sort | ✅ Injects group-key sort, deterministic |

**Code path:** `QueryableEfCoreExtensions.ApplyFlexQueryAsync` → `ExecuteGroupedQueryAsync`
→ `BuildGroupedSorts<TShape>` → `ResolveGroupedSortField`

### Dapper provider (FlexQuery.NET.Dapper)

| Scenario | Behavior |
|---|---|
| Sort by group key | ✅ Resolves correctly |
| Sort by aggregate alias | ✅ Resolves correctly (quoted as-is) |
| Sort by aggregate source field | ❌ Generates SQL using entity column name (not GROUP BY safe) |
| Sort by non-projected field | ❌ Generates SQL using entity column name (invalid SQL) |
| All sorts invalid | ❌ Generates invalid SQL |
| Paging without sort | ❌ No ORDER BY generated, nondeterministic |

**Code path:** `SqlTranslator.Translate` → `BuildOrderByClause` → `ResolveOrderByExpression`

---

## Risks

### 1. Dapper generates invalid SQL for grouped queries

When a sort field references a mapped entity column that is not in the GROUP BY,
`ResolveOrderByExpression` resolves it to the physical column name. This produces:

```sql
SELECT [CustomerId], SUM([Total]) AS [totalSum]
FROM [Orders]
GROUP BY [CustomerId]
ORDER BY [Id]    -- Id is NOT in GROUP BY -> SQL error
```

**Severity:** High — causes runtime SQL exceptions

### 2. EF Core silently removes invalid sorts

When a sort field cannot be resolved against the grouped projection type, it is silently
skipped. The consumer receives no error or warning.

**Severity:** Medium — consumer may not realize sort was ignored

### 3. Dapper paging nondeterministic without sort

When Sort is empty, `BuildOrderByClause` returns empty and no ORDER BY is generated.
Paging via LIMIT/OFFSET without ORDER BY produces nondeterministic results.

**Severity:** High — produces incorrect/inconsistent paging

### 4. Provider inconsistency

EF Core resolves aggregate field names to aggregate aliases. Dapper uses the raw entity
column name. This means the same query produces different ORDER BY across providers:

| Sort field | EF Core ORDER BY | Dapper ORDER BY |
|---|---|---|
| Total (when SUM(Total) AS totalSum) | totalSum | Total |

**Severity:** Medium — consumers switching providers get different behavior

---

## Recommendations

### Short term (aligned with this investigation)

1. **Create a shared `GroupedSortValidator`** in FlexQuery.Core that both providers use,
   implementing rules 1–3 above (valid fields, silent removal, fallback when empty).
2. **Replace EF Core's inline `BuildGroupedSorts` / `ResolveGroupedSortField`** with the
   shared validator.
3. **Add grouped sort validation to the Dapper SQL translator** before `BuildOrderByClause`,
   using the shared validator.
4. **Fix Dapper `ResolveOrderByExpression` in grouped context** to quote the field as-is
   (order by alias) rather than looking up the entity column name.

### Long term (future consideration)

- Consider adding a **warning callback** or **logging** when sorts are silently removed,
  so consumers can detect the issue.
- Consider whether aggregate field → alias resolution should apply to Filter as well
  (e.g., `Price > 100` in a grouped query resolving to `priceAvg > 100`).

---

## Appendix: Code References

| File | Line(s) | Description |
|---|---|---|
| `src/FlexQuery.NET/Builders/QueryBuilder.cs` | 35–71 | ApplySort — applies sorts via expression trees |
| `src/FlexQuery.NET/Builders/QueryBuilder.cs` | 76–107 | ApplyPaging — fallback sort when Skip > 0 and unordered |
| `src/FlexQuery.NET/Builders/GroupByBuilder.cs` | 22–80 | Grouped projection builder |
| `src/FlexQuery.NET.EntityFrameworkCore/Extensions/QueryableEfCoreExtensions.cs` | 249–305 | EF Core grouped execution, BuildGroupedSorts, ResolveGroupedSortField |
| `src/FlexQuery.NET.Dapper/SQL/Translators/SqlTranslator.cs` | 204–223 | Dapper BuildOrderByClause, ResolveOrderByExpression |
| `src/FlexQuery.NET.Adapters.AgGrid/Parsers/AgGridQueryOptionsParser.cs` | 108–153 | AG Grid's ValidateGroupedSorts (reference) |
