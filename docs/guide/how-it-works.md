# How FlexQuery.NET Works

FlexQuery.NET sits between your HTTP controller and your `IQueryable`. It translates client-provided query parameters into validated, server-safe LINQ expression trees that EF Core compiles to SQL.

---

## The Pipeline at a Glance

```
HTTP Query String
        │
        ▼
  FlexQueryParameters          ← Plain DTO bound from [FromQuery]
        │
        ▼
  QueryOptionsParser.Parse()   ← Auto-detects DSL / JQL / JSON / Indexed format
        │
        ▼
      QueryOptions              ← Internal AST: FilterGroup, SortNode[], PagingOptions, etc.
        │
        ├── ValidateOrThrow<T>(execOptions)
        │       ├── Field depth check
        │       ├── Blocked fields check
        │       ├── Role-based access check
        │       ├── Operation-level rules (filterable, sortable, selectable)
        │       └── Global AllowedFields check
        │
        ├── ApplyFilter()        → WHERE clause (expression tree)
        ├── ApplySort()          → ORDER BY (expression tree)
        ├── CountAsync()         → SELECT COUNT(*) (optional)
        ├── ApplyPaging()        → SKIP / TAKE
        ├── ApplyFilteredIncludes() → Include pipeline (independent)
        └── ApplySelect()        → Dynamic projection
                  │
                  ▼
           QueryResult<object>
      { data, totalCount, page, pageSize }
```

---

## Step 1: Parsing

The `QueryOptionsParser` reads your client's query parameters and builds a `QueryOptions` object.

It automatically detects which format was used:

| Format | Detection | Example |
| :--- | :--- | :--- |
| **DSL** | `filter=` with colon syntax | `filter=status:eq:active` |
| **JQL** | `query=` parameter present | `query=status = "active"` |
| **JSON** | `filter=` value starts with `{` | `filter={"logic":"and",...}` |
| **Indexed** | Keys like `filter[0].field=` | `filter[0].field=status` |

The parsed output is a structured `QueryOptions`:

```csharp
// filter=status:eq:active&sort=name:asc&page=2&pageSize=10
// Parses to:
new QueryOptions
{
    Filter = new FilterGroupNode
    {
        Logic    = LogicOperator.And,
        Children = [
            new FilterConditionNode { Field = "status", Operator = "eq", Value = "active" }
        ]
    },
    Sort = [ new SortNode { Field = "name", Descending = false } ],
    Paging = new PagingOptions { Page = 2, PageSize = 10 }
}
```

---

## Step 2: Validation

Before any expression is built, the validation pipeline runs against the parsed `QueryOptions`.

It checks **every field** used in filters, sorts, and projections against your server-side `QueryExecutionOptions`.

The validator walks the full filter AST — including nested AND/OR groups and collection predicates — to validate every field path it encounters.

```csharp
var execOptions = new QueryExecutionOptions
{
    AllowedFields = new HashSet<string> { "id", "name", "status" },
    BlockedFields = new HashSet<string> { "passwordHash" },
    MaxFieldDepth = 2
};

options.ValidateOrThrow<User>(execOptions);
// Throws QueryValidationException if any field violates a rule
```

---

## Step 3: Expression Tree Construction

The `ExpressionBuilder` compiles the `FilterGroup` AST into a LINQ `Expression<Func<T, bool>>`.

For example, `status:eq:active AND age:gte:18` becomes:

```csharp
// Conceptually equivalent to:
Expression<Func<User, bool>> predicate = u =>
    u.Status == "active" && u.Age >= 18;
```

This is done entirely through `System.Linq.Expressions` — no string concatenation, no `eval`. The result is a strongly-typed expression tree that any `IQueryable` provider can process.

For collection predicates like `orders:any:status:eq:shipped`:

```csharp
Expression<Func<User, bool>> predicate = u =>
    u.Orders.AsQueryable().Any(o => o.Status == "shipped");
```

EF Core translates this to an `EXISTS` subquery.

---

## Step 4: IQueryable Pipeline

The expressions are applied to your `IQueryable` using standard LINQ methods:

```csharp
query = query.Where(filterPredicate);     // ApplyFilter
query = query.OrderBy(keySelector);       // ApplySort
query = query.Skip(skip).Take(take);      // ApplyPaging
query = query.Include(...).Where(...);    // ApplyFilteredIncludes
var projected = query.Select(projection); // ApplySelect
```

**No query executes at this stage.** This is standard LINQ deferred execution. The `IQueryable` is extended with new expression nodes — nothing hits the database until materialization.

---

## Step 5: SQL Generation & Execution

When you call `ToListAsync()` or `CountAsync()`, EF Core walks the complete expression tree and generates a single parameterized SQL query.

For `filter=status:eq:active&sort=name:asc&page=1&pageSize=10&select=id,name,email`:

```sql
SELECT TOP(10) [u].[Id], [u].[Name], [u].[Email]
FROM [Users] AS [u]
WHERE [u].[Status] = N'active'
ORDER BY [u].[Name] ASC
```

Everything happens server-side. FlexQuery.NET never loads data into memory to filter it.

---

## Two Independent Pipelines

FlexQuery.NET has **two separate pipelines** that operate independently:

### WHERE Pipeline
Handles root entity filtering, sorting, and paging.

```csharp
query = ApplyFilter(query, options);
query = ApplySort(query, options);
query = ApplyPaging(query, options);
```

### Include Pipeline
Handles related collection loading with optional filters.

```csharp
query = query.ApplyFilteredIncludes(options);
```

The include pipeline is **completely independent** from the WHERE pipeline. Filtering a collection inside `include=Orders(status:eq:shipped)` does **not** affect which root entities are returned.

---

## Expression Caching

For high-throughput APIs, FlexQuery.NET can cache compiled expression trees:

```csharp
options.EnableCache = true;
```

The `FilterNormalizer` canonicalizes the filter AST before generating a cache key. This means semantically equivalent queries share the same cache entry — even if field order differs:

```
filter=status:eq:active,age:gte:18
filter=age:gte:18,status:eq:active
```

Both produce the same normalized form → same cache key → same compiled predicate.

---

## Key Properties

- **Expression-based**: Every operation is a LINQ expression tree — never a string.
- **Deferred execution**: No database call until you call `ToListAsync` or `CountAsync`.
- **EF Core native**: Works with any `IQueryable` provider; optimized for EF Core.
- **Composable**: Chain your own `.Where()` before or after FlexQuery's pipeline steps.
- **Zero reflection at runtime**: Projection and filtering are compiled at parse time, not per-row.
