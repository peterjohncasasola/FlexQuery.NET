# Migration Guide: v2 → v3

FlexQuery.NET v3.0.0 introduces a **modular, provider-agnostic architecture** along with new first-party integrations. The query engine is decoupled from Entity Framework, and JQL parsing has been extracted into an install-on-demand package.

---

## 🔥 Breaking Changes Summary

### 1. JQL Parser Extracted to Separate Package

The JQL parser has been **removed from FlexQuery.NET Core** and is now available as `FlexQuery.NET.Parsers.Jql`.

| Before (v2.x) | After (v3) |
|:---|:---|
| JQL parser bundled in `FlexQuery.NET` | Must install `dotnet add package FlexQuery.NET.Parsers.Jql` |
| `QuerySyntax.Jql` enum value existed | Enum value removed — JQL is an external parser |
| `FilteredIncludeParser` fell back to JQL for inline filters | Inline include filters require **DSL syntax** only (`field:op:value`) |

#### JQL Migration Steps

```bash
# Install the JQL package if you use query=... parameters
dotnet add package FlexQuery.NET.Parsers.Jql
```

**If you used `query=` parameter in v2:**

```csharp
// Before (v2 — JQL bundled in Core)
var options = QueryOptionsParser.Parse(new Dictionary<string, string>
{
    ["query"] = "status = 'Active' AND amount > 1000"
});

// After (v3 — use JqlParser directly)
using FlexQuery.NET.Parsers.Jql;

var filter = new JqlParser().Parse("status = 'Active' AND amount > 1000");
var options = new QueryOptions { Filter = filter };
```

**If you used inline include filters:**

```csharp
// Before (v2 — JQL syntax was accepted)
var result = FilteredIncludeParser.Parse("orders(Status = 'Cancelled')");

// After (v3 — DSL syntax required)
var result = FilteredIncludeParser.Parse("orders(Status:eq:Cancelled)");
```

### 2. JQL `Parse(string query)` → `Parse(string filter)`

The parameter name in `JqlParser.Parse()` and `IQueryParser.Parse()` has been renamed from `query` to `filter` to align with DSL and MiniOData conventions.

```csharp
// Before
new JqlParser().Parse(query: "status = 'Open'");

// After
new JqlParser().Parse(filter: "status = 'Open'");
```

> [!NOTE]
> Callers using positional arguments (e.g., `new JqlParser().Parse("status = 'Open'")`) do not need any change.

### 3. Package Rename: EFCore → EntityFrameworkCore

`FlexQuery.NET.EFCore` has been renamed to `FlexQuery.NET.EntityFrameworkCore`:

```bash
dotnet remove package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.EntityFrameworkCore
```

Namespace changes:

```csharp
// Before
using FlexQuery.NET.EFCore;

// After
using FlexQuery.NET.EntityFrameworkCore;
```

> The AG Grid and Kendo adapter packages were also renamed (`FlexQuery.NET.AgGrid` → `FlexQuery.NET.Adapters.AgGrid`, `FlexQuery.NET.Kendo` → `FlexQuery.NET.Adapters.Kendo`). Since these packages were introduced as part of v3.0.0 with no prior releases, no migration action is needed — simply install the new names.

### 4. Removed APIs

| Removed API | Replacement | Notes |
|:---|:---|:---|
| `QueryRequest` | `FlexQueryParameters` | Deprecated in v2, removed in v3 |
| `FlexQueryRequest` | `FlexQueryParameters` | Deprecated in v2, removed in v3 |
| `QuerySyntax.Jql` | N/A | JQL is now an external parser |
| `ApplyValidatedQueryOptions` | `FlexQueryAsync` | Deprecated in v2 |
| `ToQueryResultAsync` | `FlexQueryAsync` | Deprecated in v2 |
| `ToProjectedQueryResultAsync` | `FlexQueryAsync` | Deprecated in v2 |

### 5. Target Framework Changes

- **Added:** `net10.0`
- **Removed:** `net7.0` (EOL)
- **Supported:** `net6.0`, `net8.0`, `net10.0`

---

## 🆕 What's New in v3

### New Packages

| Package | Description |
|:---|:---|
| `FlexQuery.NET.Dapper` | Dapper and raw SQL provider with dialect support (SQL Server, SQLite, MySQL, PostgreSQL) |
| `FlexQuery.NET.Parsers.MiniOData` | OData-style `$filter`, `$orderby`, `$select`, `$top`/`$skip` parser |
| `FlexQuery.NET.Parsers.Jql` | Extracted JQL parser (install-on-demand) |
| `FlexQuery.NET.Adapters.AgGrid` | AG Grid Enterprise Server-Side Row Model adapter |
| `FlexQuery.NET.Adapters.Kendo` | Kendo UI DataSource adapter |



## ResultCount Added

`QueryResult<T>` now includes an optional `ResultCount` property:

```csharp
public int? ResultCount { get; init; }
```

This is additive and backward compatible. Existing code that reads `TotalCount`, `Page`, `PageSize`, or `Data` continues to work.

### Count Semantics

| Property | Meaning |
| ----------- | ------------------------- |
| `TotalCount` | Filtered source records |
| `ResultCount` | Shaped rows before paging |
| `Data.Count` | Current page rows |

`TotalCount` semantics are unchanged. It still represents the number of source records after filtering and before paging.

`ResultCount` represents the number of rows produced by the final query shape before paging. This matters for grouped, distinct, pivoted, or otherwise cardinality-changing queries.

```text
1432 products
GROUP BY Brand

4 brand groups

TotalCount  = 1432
ResultCount = 4
```

```text
1432 products
DISTINCT Brand

12 brands

TotalCount  = 1432
ResultCount = 12
```

For grouped UI adapters such as AG Grid SSRM, prefer:

```csharp
var rowCount = result.ResultCount ?? result.TotalCount;
```

This lets grouped result pagination use the shaped row count while preserving compatibility with older results or providers that do not populate `ResultCount`.


### Grand Total Aggregations

Grand total aggregations can now be computed alongside grouped aggregations in a single query:

```csharp
options.Aggregates.Add(new AggregateModel
{
    Field = "Price",
    Function = "sum",
    Alias = "grand_total_price"
});
```

### Aggregate Alias Convention

Aggregate aliases were redesigned in v3.0.3 from `FUNCTION_Field` to a field-first, camelCase format:

**Before:**
```csharp
options.Aggregates.Add(new AggregateModel
{
    Field = "Total",
    Function = "sum",
    Alias = "SUM_Total"   // ← old FUNCTION_Field format
});
```

**After:**
```csharp
options.Aggregates.Add(new AggregateModel
{
    Function = "sum",
    Field = "Total",
    Alias = "totalSum"    // ← new field-first camelCase format
});
```

If you rely on the built-in parsers or `BuildAggregateAlias()`, the new format is applied automatically. Only hardcoded `Alias` strings need updating.

**Sort by aggregate:**
```csharp
// Before
options.Sort = [new SortNode { Field = "SUM_Total", Descending = true }];

// After
options.Sort = [new SortNode { Field = "totalSum", Descending = true }];
```

**Response shape change:**
```json
// Before: { "status": "active", "COUNT_All": 42 }
// After:  { "status": "active", "allCount": 42 }
```

### Having Support

Filter grouped/aggregated results using the `Having` clause:

```csharp
options.Having = new FilterGroup
{
    Filters =
    {
        new FilterCondition { Field = "Price", Operator = "gt", Value = "1000" }
    }
};
```

### Non-Strict Validation

Set `StrictFieldValidation = false` to silently remove unauthorized fields instead of throwing:

```csharp
await _context.Products.FlexQueryAsync<Product>(parameters, exec =>
{
    exec.StrictFieldValidation = false; // Removes unauthorized fields gracefully
});
```

### Flat Projection Support

New flattening modes for Dapper and raw SQL providers:

| Mode | Description |
|:---|:---|
| `mode=flat` | Flattens nested objects into a single row |
| `mode=flat-mixed` | Flattens while preserving collection structure |

### DTO Field Mapping

Map incoming fields to different database columns:

```csharp
options.MapField("customer_name", "CustomerName");
options.MapField("product_price", "UnitPrice");
```

---

## 📦 Package Restructuring

v3 reorganizes FlexQuery.NET into focused packages. Libraries that were previously monolithic are now modular:

```
FlexQuery.NET (Core)
├── FlexQuery.NET.EntityFrameworkCore (renamed from FlexQuery.NET.EFCore)
├── FlexQuery.NET.AspNetCore      (unchanged)
├── FlexQuery.NET.Dapper          (new)
├── FlexQuery.NET.Parsers.Jql     (extracted from Core)
├── FlexQuery.NET.Parsers.MiniOData (new)
├── FlexQuery.NET.Adapters.AgGrid (new)
└── FlexQuery.NET.Adapters.Kendo  (new)
```

---

## 🚀 Upgrade Steps

### Step 1: Update Target Framework

Ensure your projects target `net6.0`, `net8.0`, or `net10.0`. Remove `net7.0` references.

### Step 2: Update Package References

```bash
# Core packages
dotnet add package FlexQuery.NET
dotnet add package FlexQuery.NET.EntityFrameworkCore
dotnet add package FlexQuery.NET.AspNetCore

# Optional: Install new packages as needed
dotnet add package FlexQuery.NET.Dapper
dotnet add package FlexQuery.NET.Parsers.Jql
dotnet add package FlexQuery.NET.Parsers.MiniOData
dotnet add package FlexQuery.NET.Adapters.AgGrid
dotnet add package FlexQuery.NET.Adapters.Kendo
```

> If upgrading from v2, first remove the old `FlexQuery.NET.EFCore` reference:
> ```bash
> dotnet remove package FlexQuery.NET.EFCore
> ```

### Step 3: Remove Deprecated API Usages

Replace any remaining `QueryRequest` or `FlexQueryRequest` with `FlexQueryParameters`:

```csharp
// Before
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)

// After
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
```

Replace `ToQueryResultAsync` / `ToProjectedQueryResultAsync` with `FlexQueryAsync`:

```csharp
// Before
var result = await query.ToQueryResultAsync(options);
var projected = await query.ToProjectedQueryResultAsync(options);

// After
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec => { ... });
```

### Step 4: Install JQL Parser If Needed

If your application uses `query=` parameters or JQL syntax, install the separate package:

```bash
dotnet add package FlexQuery.NET.Parsers.Jql
```

### Step 5: Update Inline Include Filters

If you use filtered includes with JQL-style syntax, update to DSL:

```csharp
// Before (v2 — JQL fallback)
"orders(Status = 'Cancelled')"

// After (v3 — DSL only)
"orders(Status:eq:Cancelled)"
```

---

## 🛠️ Common Migration Pitfalls

### ❌ Missing JQL Package

```
// ERROR: The type or namespace 'JqlParser' does not exist
```

Install the package: `dotnet add package FlexQuery.NET.Parsers.Jql`

### ❌ Inline Include Filter Still Using JQL Syntax

```
FilteredIncludeParser.Parse("orders(Status = 'Cancelled')")  // Returns null in v3
```

Use DSL syntax instead: `FilteredIncludeParser.Parse("orders(Status:eq:Cancelled)")`

### ❌ Old EFCore Package Name

```
// ERROR: ProjectReference not found
// FlexQuery.NET.EFCore.csproj
```

The package was renamed to `FlexQuery.NET.EntityFrameworkCore`:

```bash
dotnet remove package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.EntityFrameworkCore
```

### ❌ Old EFCore Namespace

```
// ERROR: The type or namespace 'EFCore' does not exist in 'FlexQuery.NET'
```

Update `using` directives:

```csharp
// Before
using FlexQuery.NET.EFCore;

// After
using FlexQuery.NET.EntityFrameworkCore;
```

---

## 🔍 Full Changelog

See the [CHANGELOG.md](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/CHANGELOG.md) for the complete list of changes.
