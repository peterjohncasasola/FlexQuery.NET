# SQL Generation

## Overview

The SQL generation pipeline is the core of `FlexQuery.NET.Dapper`. It transforms a `QueryOptions` AST into a fully parameterized SQL string that Dapper executes directly against your database. Every value is parameterized — no string concatenation, no SQL injection vectors.

### What It Is

The `SqlTranslator` class is an orchestrator that resolves entity mappings, builds a selection tree, and delegates to specialized builders for each SQL clause. The result is a `SqlCommand` object containing the SQL string and a dictionary of named parameters.

### Why It Exists

Manually building SQL WHERE clauses from dynamic user input is tedious, error-prone, and a security risk. The SQL generation pipeline handles operator translation, parameter naming, identifier quoting, JOIN generation, and dialect-specific syntax — all from the same `QueryOptions` model that EF Core uses.

## Architecture

```
QueryOptions
     │
     ▼
SqlTranslator.Translate()
     │
     ├── PrepareTranslation()    → Resolves entity mapping + selection tree
     │
     ├── SqlSelectBuilder        → SELECT clause (columns, aliases, aggregates)
     ├── SqlWhereBuilder         → WHERE clause (filters → parameterized conditions)
     ├── SqlJoinBuilder          → JOIN clauses (includes, relationships)
     ├── BuildGroupByClause()    → GROUP BY (inline — simple logic)
     ├── BuildHavingClause()     → HAVING (inline — simple logic)
     ├── BuildOrderByClause()    → ORDER BY (inline — simple logic)
     └── BuildPagingClause()     → OFFSET/FETCH or LIMIT/OFFSET (dialect-specific)
     │
     ▼
SqlCommand { Sql, Parameters, FlatJoins }
```

### Design Decisions

The translator follows a clear delegation pattern:

- **Complex, recursive structures** (SELECT, WHERE, JOIN) are handled by dedicated builder classes with their own internal state
- **Simple, linear clauses** (GROUP BY, HAVING, ORDER BY, paging) are built inline — a dedicated class for each would be ceremony without payoff
- **All SQL generation is dialect-aware** — every identifier quote, parameter prefix, and pagination syntax goes through the `ISqlDialect` abstraction

## Parameter Generation

Every filter value is parameterized with an auto-incrementing name:

```csharp
// Input: filter=Name eq 'John' AND Age gt 25
// Generated SQL:
// WHERE [Name] = @p0 AND [Age] > @p1
// Parameters: { "@p0": "John", "@p1": 25 }
```

Parameter names use the dialect's prefix:
- SQL Server: `@p0`, `@p1`
- PostgreSQL: `:p0`, `:p1`
- MySQL: `?p0`, `?p1`

The `SqlParameterContext` tracks all parameters and ensures unique naming across the entire query.

## Projection Generation

### Standard Projection

When `Select` fields are specified, only those columns appear in the SELECT clause:

```
?select=Id,Name,Email

→ SELECT [Id], [Name], [Email] FROM [Users]
```

### Flat Projection Mode

When `ProjectionMode` is `Flat` or `FlatMixed`, the translator generates LEFT JOINs and aliases:

```
?select=Id,Name,Orders.Id,Orders.Total&mode=flat

→ SELECT [Users].[Id], [Users].[Name],
         [Orders].[Id] AS [Orders_Id], [Orders].[Total] AS [Orders_Total]
  FROM [Users]
  LEFT JOIN [Orders] ON [Users].[Id] = [Orders].[UserId]
```

### Aggregate Projection

When aggregates are requested with GROUP BY:

```
?groupBy=Category&aggregate=sum(Price),count(Id)

→ SELECT [Category], SUM([Price]) AS sum_Price, COUNT([Id]) AS count_Id
  FROM [Products]
  GROUP BY [Category]
```

## Ordering Generation

Sort nodes translate directly to ORDER BY clauses:

```
?sort=Name:asc,CreatedAt:desc

→ ORDER BY [Name], [CreatedAt] DESC
```

When sorting by aggregate fields, the aggregate expression is used in the ORDER BY.

## Paging Generation

Paging is fully dialect-aware:

```
?page=2&pageSize=20
```

| Dialect | Generated SQL |
|---------|--------------|
| SQL Server | `OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY` |
| PostgreSQL | `LIMIT :PageSize OFFSET :Offset` |
| MySQL | `LIMIT ?PageSize OFFSET ?Offset` |
| Oracle (12c+) | `OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY` |
| SQLite | `LIMIT @PageSize OFFSET @Offset` |

Offset and limit values are always parameterized, never inlined.

## Basic Example

```csharp
var parameters = new FlexQueryParameters
{
    Filter = "Status eq 'Active' AND Age gt 18",
    Sort = "Name:asc",
    Page = 1,
    PageSize = 20,
    Select = "Id,Name,Email"
};

var result = await connection.FlexQueryAsync<User>(parameters, opts =>
{
    opts.Dialect = new SqlServerDialect();
});

// Generated SQL:
// SELECT [Id], [Name], [Email]
// FROM [Users]
// WHERE [Status] = @p0 AND [Age] > @p1
// ORDER BY [Name]
// OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
//
// Parameters: { "@p0": "Active", "@p1": 18, "@Offset": 0, "@PageSize": 20 }
```

## Advanced Example: Aggregation with HAVING

```csharp
var options = new QueryOptions
{
    GroupBy = new List<string> { "Category" },
    Aggregates = new List<AggregateModel>
    {
        new() { Function = "sum", Field = "Price", Alias = "sum_Price" },
        new() { Function = "count", Field = "Id", Alias = "count_Id" }
    },
    Having = new HavingCondition
    {
        Function = "sum",
        Field = "Price",
        Operator = "gt",
        Value = "1000"
    }
};

// Generated SQL:
// SELECT [Category], SUM([Price]) AS sum_Price, COUNT([Id]) AS count_Id
// FROM [Products]
// GROUP BY [Category]
// HAVING SUM([Price]) > @p0
```

## Grand Total Aggregation

When aggregates are requested **without** GROUP BY, FlexQuery generates a separate aggregate query to compute grand totals:

```csharp
var options = new QueryOptions
{
    Aggregates = new List<AggregateModel>
    {
        new() { Function = "sum", Field = "Total", Alias = "sum_Total" },
        new() { Function = "avg", Field = "Total", Alias = "avg_Total" }
    }
};

// Main query: SELECT * FROM [Orders] ...
// Aggregate query (via TranslateAggregates):
// SELECT SUM([Total]) AS sum_Total, AVG([Total]) AS avg_Total FROM [Orders]
```

The results are returned in `QueryResult<T>.Aggregates` as a nested dictionary:
```json
{
  "Total": { "sum": 15000, "avg": 250 }
}
```

## Performance Considerations

- **Parameterization** enables SQL plan caching — the same query structure with different values reuses the same execution plan
- **Flat projection** generates a single query with JOINs instead of N+1 round trips
- **COUNT queries** are only executed when pagination data suggests there might be more pages
- The translator does **not** cache SQL strings — each call generates fresh SQL. The overhead is negligible (microseconds) compared to database round-trip time

## Security Considerations

- All values are parameterized — **SQL injection is structurally impossible**
- Identifier quoting prevents column name injection (e.g., `[User Input]` is treated as a column name, not SQL code)
- Combine with `AllowedFields` validation to prevent schema enumeration via error messages

## Best Practices

1. **Always use `AllowedFields`** — The translator generates SQL for whatever fields are in the QueryOptions. Validation must happen before translation.
2. **Profile generated SQL** — Use SQL Server Profiler, `pg_stat_statements`, or equivalent to verify the generated queries are efficient
3. **Add indexes** for frequently filtered and sorted columns
4. **Use `CommandTimeoutSeconds`** to prevent runaway queries from expensive filter combinations

## Related Features

- [Getting Started](/providers/dapper/getting-started) — Installation and first query
- [Dialects](/providers/dapper/dialects) — Database-specific SQL rules
- [Relationship Queries](/providers/dapper/relationship-queries) — JOIN and EXISTS generation
