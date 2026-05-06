# Core Concepts



## API Levels

FlexQuery.NET exposes two complementary API layers:

| API Level | Recommended For | Entry Point |
|---|---|---|
| High-Level API | Controllers, APIs, frontend-driven filtering | `FlexQueryParameters + FlexQuery()` |
| Advanced API | Server-side query composition, strongly-typed filters | `QueryOptions + ApplyQueryOptions()` |

Most applications should use the high-level API.

The advanced API exists for scenarios requiring:
- programmatic query construction
- dynamic filter composition
- nested logical query trees
- reusable server-side query templates

---

## Understanding the Core Model

Understanding the core model helps you use FlexQuery.NET correctly and avoid common mistakes.

---

## The Execution Pipeline

Every query in FlexQuery.NET flows through the same pipeline:

```
HTTP Query String
       │
       ▼
 FlexQueryParameters          ← Public DTO, bound from [FromQuery]
       │
       ▼
 QueryOptionsParser.Parse()   ← Detects format, builds AST
       │
       ▼
     QueryOptions              ← The internal parsed model
       │
       ├── ValidateOrThrow<T>() ← Field access, operator, depth checks
       │
       ├── ApplyFilter()        ← Expression tree → SQL WHERE
       ├── ApplySort()          ← Expression tree → SQL ORDER BY
       ├── ApplyPaging()        ← SKIP / TAKE
       ├── ApplyFilteredIncludes() ← Include pipeline
       └── ApplySelect()        ← Dynamic projection
                 │
                 ▼
          QueryResult<object>
     { data, totalCount, page, pageSize }
```

---

## FlexQueryParameters

`FlexQueryParameters` is the **public API contract** — the DTO your clients interact with.

It is a plain C# class. Bind it directly from `[FromQuery]`:

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters) { ... }
```

| Property | Type | Purpose |
| :--- | :--- | :--- |
| `Filter` | `string?` | DSL or JSON filter expression |
| `Query` | `string?` | JQL-style filter (`query=name = "alice"`) |
| `Sort` | `string?` | Sort expression (`name:asc,age:desc`) |
| `Select` | `string?` | Comma-separated fields to project |
| `Includes` | `string?` | Navigation properties to include |
| `GroupBy` | `string?` | Fields to group by |
| `Having` | `string?` | Aggregate condition on groups |
| `Page` | `int?` | Page number (1-indexed) |
| `PageSize` | `int?` | Items per page |
| `IncludeCount` | `bool?` | Whether to return total count |
| `Distinct` | `bool?` | Apply DISTINCT |
| `Mode` | `string?` | Projection mode: `nested`, `flat`, `flat-mixed` |

---

## QueryOptions

`QueryOptions` is the **internal parsed representation** of a client's request.

`QueryOptions` is primarily produced by `QueryOptionsParser.Parse()`,
but advanced users may also construct it manually for programmatic query composition.

```csharp
var options = QueryOptionsParser.Parse(parameters);
```

Key properties:

| Property | Type | Description |
| :--- | :--- | :--- |
| `Filter` | `FilterGroup?` | Parsed filter AST (nested AND/OR tree) |
| `Sort` | `List<SortNode>` | Ordered list of sort fields and directions |
| `Select` | `List<string>?` | Projected field paths |
| `Includes` | `List<string>?` | Navigation properties to include |
| `FilteredIncludes` | `List<IncludeNode>?` | Structured include tree with inline filters |
| `Paging` | `PagingOptions` | Page number, page size, skip offset |
| `GroupBy` | `List<string>?` | Group-by field paths |
| `Aggregates` | `List<AggregateModel>` | Aggregate expressions (sum, count, avg) |
| `Having` | `HavingCondition?` | HAVING clause for aggregate filtering |
| `ProjectionMode` | `ProjectionMode` | Nested / Flat / FlatMixed |
| `Distinct` | `bool?` | Apply DISTINCT |
| `CaseInsensitive` | `bool` | Whether string comparisons are case-insensitive |
| `IncludeCount` | `bool?` | Whether to run a COUNT query |

---

## QueryExecutionOptions

`QueryExecutionOptions` contains **server-side constraints** — not client-provided.

You create it in your controller and pass it to `ValidateOrThrow<T>()` or `FlexQueryAsync`.

```csharp
var execOptions = new QueryExecutionOptions
{
    AllowedFields     = new HashSet<string> { "id", "name", "email" },
    BlockedFields     = new HashSet<string> { "passwordHash" },
    FilterableFields  = new HashSet<string> { "name", "status" },
    SortableFields    = new HashSet<string> { "name", "createdAt" },
    SelectableFields  = new HashSet<string> { "id", "name", "email" },
    MaxFieldDepth     = 2,
    StrictFieldValidation = true
};
```

| Property | Type | Description |
| :--- | :--- | :--- |
| `AllowedFields` | `HashSet<string>?` | Global allow-list (all operations) |
| `BlockedFields` | `HashSet<string>?` | Explicitly blocked fields |
| `FilterableFields` | `HashSet<string>?` | Fields allowed in filter expressions |
| `SortableFields` | `HashSet<string>?` | Fields allowed in sort expressions |
| `SelectableFields` | `HashSet<string>?` | Fields allowed in select/projection |
| `MaxFieldDepth` | `int?` | Maximum dot-notation path depth |
| `FieldMappings` | `Dictionary<string, string>?` | Field alias → real field mapping |
| `FieldAccessResolver` | `IFieldAccessResolver?` | Custom programmatic resolver |
| `StrictFieldValidation` | `bool` | Throw on access violation (vs. collect errors) |

---


## Parsing Formats

`QueryOptionsParser` auto-detects the input format:

### DSL Format
```
GET /api/users?filter=status:eq:active&sort=name:asc&page=1&pageSize=10
```

### JQL Format
```
GET /api/users?query=status = "active" AND age >= 18&sort=name:asc
```

### JSON Format
```
GET /api/users?filter={"logic":"and","filters":[{"field":"status","operator":"eq","value":"active"}]}
```

### Indexed Format
```
GET /api/users?filter[0].field=status&filter[0].operator=eq&filter[0].value=active
```

---

## Validation

Validation runs against the `QueryOptions` AST, not the raw string. It checks:

1. **Field existence** — Does the field exist on the entity type?
2. **Field access** — Is the field in the `AllowedFields` / `FilterableFields` lists?
3. **Operator validity** — Is the operator compatible with the field type?
4. **Depth** — Does the dot-notation path exceed `MaxFieldDepth`?
5. **Blocked fields** — Is the field explicitly in `BlockedFields`?

```csharp
// Option 1: Throw on first failure
options.ValidateOrThrow<User>(execOptions);

// Option 2: Collect all errors
var result = options.ValidateSafe<User>(execOptions);
if (!result.IsValid)
{
    // result.Errors is a List<ValidationError>
    return BadRequest(result.Errors);
}
```

---

## Projection

FlexQuery.NET supports three projection modes:

### Nested (Default)

Preserves the object hierarchy:

```json
{
  "id": 1,
  "name": "Alice",
  "profile": {
    "bio": "Developer"
  }
}
```

### Flat

Flattens all properties to top-level with dot-notation keys:

```json
{
  "id": 1,
  "name": "Alice",
  "profile.bio": "Developer"
}
```

### FlatMixed

Scalars at the top level, collections remain nested:

```json
{
  "id": 1,
  "name": "Alice",
  "profile_bio": "Developer"
}
```

---

## QueryResult

Every high-level method returns a `QueryResult<T>`:

```json
{
  "data": [ ... ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20
}
```

| Property | Type | Description |
| :--- | :--- | :--- |
| `data` | `List<T>` | The current page of results |
| `totalCount` | `int?` | Total records before paging (null if `IncludeCount=false`) |
| `page` | `int` | Current page number |
| `pageSize` | `int` | Items per page |
