# Provider Architecture

## Overview

FlexQuery's architecture separates **parsing**, **validation**, and **execution** into distinct layers. The parsing and validation layers are shared across all providers. Only the execution layer differs between EF Core and Dapper — and even there, the input (`QueryOptions`) and output (`QueryResult<T>`) are identical.

This means your security rules, field mappings, validation logic, and query syntax support work identically regardless of which database access technology you use.

## Architecture Diagram

```
FlexQueryParameters (raw input from any source)
        │
        ▼
    Parser Layer (shared)
    ├── FQLQueryParser
    ├── JsonQueryParser
    ├── DslQueryParser
    ├── MiniODataQueryParser (optional)
    └── AgGridQueryOptionsParser (optional)
        │
        ▼
    QueryOptions (canonical AST)
        │
        ▼
    Validation Layer (shared)
    ├── AllowedFields / BlockedFields
    ├── FilterableFields / SortableFields
    ├── AllowedOperators
    ├── MaxFieldDepth
    ├── StrictFieldValidation
    └── IFieldAccessResolver
        │
        ▼
    Provider Execution
        │
        ├── EF Core Provider
        │       │
        │       ▼
        │   IQueryable<T>
        │       │
        │       ├── ApplyFilter()      → Expression<Func<T, bool>>
        │       ├── ApplySort()        → OrderBy/ThenBy expressions
        │       ├── ApplyPaging()      → Skip/Take
        │       ├── ApplySelect()      → Dynamic projection
        │       └── ApplyExpand() → Include/ThenInclude
        │       │
        │       ▼
        │   EF Core → SQL (via database provider)
        │
        └── Dapper Provider
                │
                ▼
            SqlTranslator
                │
                ├── SqlSelectBuilder   → SELECT clause
                ├── SqlWhereBuilder    → WHERE clause
                ├── SqlJoinBuilder     → JOIN clauses
                ├── BuildGroupBy()     → GROUP BY
                ├── BuildHaving()      → HAVING
                ├── BuildOrderBy()     → ORDER BY
                └── BuildPaging()      → OFFSET/FETCH or LIMIT/OFFSET
                │
                ▼
            SqlCommand { Sql, Parameters }
                │
                ▼
            Dapper → Database
        │
        ▼
    QueryResult<T> (identical output shape)
```

## Shared Query Model

The `QueryOptions` class is the universal representation of a query request. Both providers consume it identically:

```csharp
public class QueryOptions
{
    public FilterGroup? Filter { get; set; }
    public List<SortNode> Sort { get; set; }
    public List<string>? Select { get; set; }
    public List<string>? Includes { get; set; }
    public List<IncludeNode>? FilteredIncludes { get; set; }
    public ProjectionMode ProjectionMode { get; set; }
    public List<string>? GroupBy { get; set; }
    public List<AggregateModel> Aggregates { get; set; }
    public HavingCondition? Having { get; set; }
    public PagingOptions Paging { get; set; }
    public bool CaseInsensitive { get; set; }
}
```

## Shared Validation Pipeline

Validation runs **before** provider execution. The same rules apply regardless of provider:

```csharp
// This validation is identical for EF Core and Dapper
options.ValidateOrThrow<User>(execOptions);
```

The validation pipeline checks:
1. Field names against `AllowedFields` / `BlockedFields`
2. Filter operators against `AllowedOperators`
3. Include paths against `AllowedIncludes`
4. Field depth against `MaxFieldDepth`
5. Page size against `MaxPageSize`

## Provider Differences

| Capability | EF Core | Dapper |
|-----------|---------|--------|
| **Expression trees** | ✅ Builds LINQ expressions | ❌ Builds SQL strings |
| **Change tracking** | ✅ Optional (`UseNoTracking`) | ❌ Not applicable |
| **Filtered includes** | ✅ Via `Include().Where()` | ✅ Via JOIN + WHERE |
| **Aggregation** | ✅ Via LINQ GroupBy | ✅ Via SQL GROUP BY |
| **SQL preview** | ✅ `ToSqlPreview()` | ✅ Access `SqlCommand.Sql` directly |
| **Dialect awareness** | ❌ Handled by EF provider | ✅ Auto-detected from `DbConnection` |
| **Connection management** | ✅ Via DbContext | ❌ You manage `DbConnection` |
| **Migrations** | ✅ EF Migrations | ❌ Not applicable |

## Extensibility

### Custom Parsers

Add support for proprietary query formats by implementing `IQueryParser`:

```csharp
QueryOptionsParser.RegisterParser(new MyCustomParser());
```

### Custom Operators

Register custom filter operators via the `OperatorHandlerRegistry`:

```csharp
OperatorHandlerRegistry.Register("regex", new RegexOperatorHandler());
```

### Custom Dialects

Add support for unsupported databases by implementing `ISqlDialect`.

### Custom Conventions

Replace Dapper's mapping conventions by implementing `IEntityConvention`, `IPluralizer`, `IForeignKeyConvention`, or `IRelationshipConvention`.

## Using Both Providers

You can use EF Core and Dapper in the same application, sharing the same parsing and validation logic:

```csharp
// EF Core endpoint
[HttpGet("products")]
public async Task<IActionResult> GetProducts([FromQuery] FlexQueryParameters p)
{
    return Ok(await _context.Products.FlexQueryAsync(p, ConfigureOptions));
}

// Dapper endpoint (for a complex view that doesn't map to EF)
[HttpGet("sales-report")]
public async Task<IActionResult> GetSalesReport([FromQuery] FlexQueryParameters p)
{
    await using var conn = new SqlConnection(_connectionString);
    return Ok(await conn.FlexQueryAsync<SalesRow>(p, ConfigureOptions));
}

// Shared validation configuration
private void ConfigureOptions(BaseQueryOptions opts)
{
    opts.AllowedFields = new HashSet<string> { "Id", "Name", "Amount" };
    opts.StrictFieldValidation = true;
}
```

## Related Features

- [EF Core Provider](/providers/ef-core) — EF Core execution details
- [Dapper Getting Started](/providers/dapper/getting-started) — Dapper execution details
- [Query Syntax](/guide/query-syntax) — The parser layer
- [Security & Governance](/guide/security-governance) — The validation layer
