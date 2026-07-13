# Core Concepts

## Overview

This guide explains the foundational building blocks of FlexQuery.NET. It covers the abstract syntax tree (AST) lifecycle, the public HTTP contracts, the internal configuration boundaries, and the pipeline that translates a string request into a database result.

## Why this feature exists

FlexQuery is a pipeline engine. To effectively debug complex projection queries, use keyset pagination, or configure strict security boundaries, developers must understand the difference between the client's intent (`FlexQueryParameters`), the parsed model (`QueryOptions`), and the server's policy (`BaseQueryOptions`). 

## When to use

- Read this page when you want to move beyond basic `FlexQueryAsync` usage and understand how to manually orchestrate the pipeline.
- Refer to this page to understand the exact JSON envelopes and projection modes supported out-of-the-box.

---

## API Levels

FlexQuery.NET exposes two complementary API layers:

| API Level | Recommended For | Entry Point |
| :--- | :--- | :--- |
| **High-Level API** | Controllers, APIs, frontend-driven filtering | `FlexQueryParameters` + `FlexQueryAsync()` (EF Core/Dapper) or `FlexQuery()` (Core) |
| **Advanced API** | Server-side query composition, strongly-typed filters | `QueryOptions` + `ApplyFilter()`, `ApplySort()` |

Most applications should use the high-level API.

The advanced API exists for scenarios requiring:
- programmatic query construction (e.g., hardcoding a multi-tenant tenant ID filter)
- dynamic filter composition outside of HTTP
- nested logical query trees
- reusable server-side query templates

---

## The Execution Pipeline

Every query in FlexQuery.NET flows through a strict lifecycle:

```text
HTTP Query String
       │
       ▼
 FlexQueryParameters          ← Public DTO, bound from [FromQuery]
       │
       ▼
   ToQueryOptions()           ← Detects format, builds AST
       │
       ▼
     QueryOptions             ← The internal parsed model
       │
       │   (FlexQueryAsync includes validation automatically)
       │
       ├── ApplyFilter()      ← AST → SQL WHERE / Expression Tree
       ├── ApplySort()        ← AST → SQL ORDER BY
       ├── ApplyPaging()      ← SKIP / TAKE or Keyset Cursor
       ├── ApplyExpand()      ← AST → SQL JOINs / EF Includes
       └── ApplySelect()      ← Dynamic projection
                 │
                 ▼
          QueryResult<T>
```

---

## `FlexQueryParameters`

`FlexQueryParameters` is the **public API contract** — the DTO your clients interact with.

It is a plain C# class. Bind it directly from `[FromQuery]`:

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters) { ... }
```

| Property | Type | Purpose |
| :--- | :--- | :--- |
| `Filter` | `string?` | DSL filter expression (`Status:eq:Active`) |
| `Sort` | `string?` | Sort expression (`LastName:asc,CreatedAt:desc`) |
| `Select` | `string?` | Comma-separated fields to project |
| `Include` | `string?` | Navigation properties to include / expand |
| `GroupBy` | `string?` | Fields to group by |
| `Having` | `string?` | Aggregate condition on groups |
| `Aggregate` | `string?` | Aggregate expressions (`SUM(TotalDue) AS Revenue`) |
| `Page` | `int?` | Page number (1-indexed) |
| `PageSize` | `int?` | Items per page |
| `IncludeCount` | `bool?` | Whether to return total count |
| `Distinct` | `bool?` | Apply DISTINCT |
| `Mode` | `string?` | Projection mode: `Nested`, `Flat`, `FlatMixed` |
| `UseKeysetPagination` | `bool` | Force keyset pagination engine |
| `Cursor` | `string?` | Keyset pagination token from a previous request |

---

## `QueryOptions`

`QueryOptions` is the **internal parsed representation** of a client's request. It represents the AST.

```csharp
QueryOptions options = parameters.ToQueryOptions();
```

Key properties:

| Property | Type | Description |
| :--- | :--- | :--- |
| `Filter` | `FilterGroup?` | Parsed filter AST (nested AND/OR tree) |
| `Sort` | `List<SortNode>` | Ordered list of sort fields and directions |
| `Select` | `List<string>?` | Projected field paths |
| `Expand` | `List<IncludeNode>?` | Structured include tree with inline filters |
| `Paging` | `PagingOptions` | Page number, page size, or cursor data |
| `GroupBy` | `List<string>?` | Group-by field paths |
| `Aggregates` | `List<AggregateModel>` | Aggregate expressions (sum, count, avg) |
| `ProjectionMode` | `ProjectionMode` | Nested / Flat / FlatMixed |

---

## `BaseQueryOptions` (Server Policy)

While `QueryOptions` represents what the *client wants*, `BaseQueryOptions` (and its derivatives like `EfCoreQueryOptions` and `DapperQueryOptions`) represents what the *server allows*.

You configure this via the lambda in `FlexQueryAsync`:

```csharp
var result = await _db.Customers.FlexQueryAsync(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "Id", "Name", "Email" };
    exec.MaxFieldDepth = 2;
    exec.StrictFieldValidation = true;
});
```

| Property | Description |
| :--- | :--- |
| `AllowedFields` | Global allow-list. If a client requests a field not on this list, it is rejected. |
| `BlockedFields` | Explicitly blocked fields (e.g. `PasswordHash`). Overrides `AllowedFields`. |
| `FilterableFields` | Fields allowed specifically in filter expressions. |
| `SortableFields` | Fields allowed specifically in sort expressions. |
| `SelectableFields` | Fields allowed specifically in projection/select expressions. |
| `GroupableFields` | Fields allowed specifically in group-by expressions. |
| `AggregatableFields` | Fields allowed specifically in aggregate expressions. |
| `AllowedIncludes` | Navigation properties allowed for include/expand. |
| `AllowedOperators` | Per-field operator restrictions. |
| `MaxFieldDepth` | Maximum dot-notation path depth. `2` allows `Category.Name` but blocks `Category.Company.Name`. |
| `MaxPageSize` | Hard cap on `pageSize` to prevent memory exhaustion. |
| `StrictFieldValidation` | `true` = Throw exception on violation. `false` = Silently strip unauthorized fields from the query. |
| `DefaultSortField` | Default sort field when client doesn't specify. |
| `DefaultSortDescending` | Default sort direction. |
| `CaseInsensitive` | Whether field name matching is case-insensitive. |
| `IncludeTotalCount` | Whether to include total count by default. |
| `ExpressionMappings` | Maps DTO field aliases to entity expressions. |
| `FieldMappings` | Maps external field aliases to internal property names. |
| `RoleAllowedFields` | Per-role field allow-lists. |
| `CurrentRole` | The active role for evaluating `RoleAllowedFields`. |
| `AllowedFieldsResolver` | Dynamic resolver for allowed fields based on entity type. |
| `Listener` | Optional execution event listener. |

---

## Parsing Formats

FlexQuery supports multiple query syntaxes. The active syntax is configured globally via `FlexQueryCore.Configure()` and parser registration. The default is **DSL** (`QuerySyntax.NativeDsl`).

### DSL Format (Default)
```http
GET /api/customers?filter=Status:eq:'Active' AND LastName:contains:Smi&sort=LastName:asc
```

### FQL Format (SQL-like)
Requires `Fql.Register()` at startup and `QuerySyntax.Fql`:
```http
GET /api/customers?filter=Status = "Active" AND LastName CONTAINS "Smi"&sort=LastName:asc
```


---

## Validation

Validation runs against the `QueryOptions` AST before any database interaction occurs. It checks:

1. **Field access** — Is the field in the `AllowedFields` list?
2. **Operator validity** — Is the operator compatible with the CLR/SQL field type?
3. **Depth** — Does the dot-notation path exceed `MaxFieldDepth`?
4. **Blocked fields** — Is the field explicitly in `BlockedFields`?

If validation fails and `StrictFieldValidation` is true, a `QueryValidationException` is thrown.

---

## Projection

FlexQuery.NET supports three projection modes when mapping relational data to JSON:

### Nested (Default)

Preserves the object hierarchy:

```json
{
  "id": 1,
  "name": "Alice",
  "address": {
    "city": "Singapore"
  }
}
```

### Flat

Flattens all properties to top-level with dot-notation keys:

```json
{
  "id": 1,
  "name": "Alice",
  "address.bio": "Developer"
}
```

### FlatMixed

Scalars at the top level, collections remain nested:

```json
{
  "id": 1,
  "name": "Alice",
  "address_city": "Singapore"
}
```

---

## QueryResult

Every high-level method returns a standardized envelope `QueryResult<T>`:

```json
{
  "totalCount": 150,
  "resultCount": 20,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice" },
    { "id": 2, "name": "Bob" }
  ],
  "nextCursorToken": null
}
```

> **Note:** The JSON properties above use camelCase due to default .NET JSON serialization. The actual C# properties on `QueryResult<T>` use PascalCase.

| C# Property | JSON Name | Type | Description |
| :--- | :--- | :--- | :--- |
| `TotalCount` | `totalCount` | `int?` | Total records before paging (null if `IncludeCount=false` or keyset pagination is used). |
| `ResultCount` | `resultCount` | `int?` | Count of records after grouping (often equals `TotalCount`). |
| `Page` | `page` | `int` | Current page number. |
| `PageSize` | `pageSize` | `int` | Items per page. |
| `TotalPages` | `totalPages` | `int` | Computed `Ceiling(ResultCount / pageSize)`. |
| `HasNextPage` | `hasNextPage` | `bool` | Whether a next page exists. |
| `HasPreviousPage` | `hasPreviousPage` | `bool` | Whether a previous page exists. |
| `Aggregates` | `aggregates` | `Dictionary<string, Dictionary<string, object>>?` | Grand total aggregate results. |
| `Data` | `data` | `IReadOnlyList<T>` | The current page of results. |
| `NextCursorToken` | `nextCursorToken` | `string?` | Keyset cursor for the next page, used in high-performance paging. |

## Best Practices
- **Never skip validation:** Whether you use `FlexQueryAsync` or the manual pipeline, never trust client query definitions blindly. Always configure an execution policy.
- **Understand Projection:** Only use `Flat` or `FlatMixed` if your frontend data-grid natively supports or requires flat dictionary data (e.g. some versions of Kendo or older AG Grid instances). `Nested` is standard REST.
