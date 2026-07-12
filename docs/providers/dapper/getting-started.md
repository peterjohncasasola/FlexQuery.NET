# Dapper Provider — Getting Started

## Overview

`FlexQuery.NET.Dapper` brings the full power of FlexQuery's dynamic querying to applications that use raw SQL and Dapper instead of Entity Framework Core. It translates `QueryOptions` into fully parameterized SQL queries, complete with dialect-specific syntax for six supported databases.

### What It Is

The Dapper provider is a SQL translation engine. It takes the same `QueryOptions` AST that EF Core uses and converts it into a parameterized SQL string that Dapper can execute. This means you can share query parsing, validation, and security logic across both EF Core and Dapper endpoints in the same application.

### Why It Exists

EF Core is powerful but not always appropriate. High-performance APIs, legacy databases, micro-ORMs, and scenarios requiring direct SQL control all benefit from Dapper. Without the Dapper provider, you would need to manually translate filter parameters into SQL WHERE clauses — a tedious and error-prone process that FlexQuery eliminates.

### When to Use It

- You use Dapper as your primary data access layer
- You need precise control over the generated SQL
- Your database schema doesn't map cleanly to an EF Core model
- You need to query views, stored procedures, or CTEs with dynamic filters
- Performance-critical paths where EF Core's overhead is unacceptable

### When NOT to Use It

- You already use EF Core and are satisfied with its performance — use `FlexQuery.NET.EntityFrameworkCore` instead
- You need change tracking, migrations, or other EF Core ORM features

## Installation

```bash
dotnet add package FlexQuery.NET.Dapper
```

This package depends on `FlexQuery.NET` (core) and `Dapper`.

## Registration

Configure the Dapper provider once at startup with the static `FlexQueryDapper.Configure` method:

```csharp
using FlexQuery.NET.Dapper;

FlexQueryDapper.Configure(opts =>
{
    // Configure entity mappings (optional — conventions will be used by default)
    opts.Model.Entity<User>().ToTable("app_users");
});
```

The `FlexQueryDapper.Configure` method builds the entity mapping model and stores it internally as the global runtime model. No DI registration is needed.

The SQL dialect is **auto-detected** from the supplied `DbConnection` at runtime — no manual dialect configuration is required.

## Your First Query

### Using FlexQueryParameters

The simplest approach — pass raw parameters and let FlexQuery parse, validate, and execute:

```csharp
[HttpGet("users")]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters)
{
    await using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();

    var result = await connection.FlexQueryAsync<User>(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "CreatedAt" };
        opts.MaxPageSize = 100;
    });

    return Ok(result);
}
```

### Using Pre-Parsed QueryOptions

When composing with adapters (AG Grid, MiniOData) or building queries programmatically:

```csharp
// Step 1: Parse from any source
var options = AgGridQueryOptionsParser.Parse(agGridRequest);

// Step 2: Execute with Dapper (dialect is auto-detected from the connection)
var result = await connection.FlexQueryAsync<User>(options, opts =>
{
    opts.AllowedFields = new HashSet<string> { "Id", "Name" };
});
```

### Using DapperQueryOptions Directly

For full control over all execution options:

```csharp
var dapperOptions = new DapperQueryOptions
{
    CommandTimeoutSeconds = 60,
    AllowedFields = new HashSet<string> { "Id", "Name" },
    StrictFieldValidation = true,
    MaxPageSize = 50
};

var result = await connection.FlexQueryAsync<User>(parameters, dapperOptions);
```

## Entity Mapping

By default, FlexQuery maps your C# class properties to database columns using conventions (class name → table name, property name → column name). For custom mappings:

```csharp
opts.Entity<User>()
    .ToTable("app_users")
    .Property(u => u.Id, "user_id")
    .Property(u => u.Name, "display_name")
    .HasMany<Order>(u => u.Orders, "user_id");
```

Or scan an entire assembly:

```csharp
opts.ScanEntitiesFromAssembly(typeof(User).Assembly);
```

See [Conventions](/providers/dapper/conventions) for the full mapping system.

## The QueryResult

All `FlexQueryAsync` overloads return a `QueryResult<T>`:

```csharp
public class QueryResult<T>
{
    public IReadOnlyList<T> Data { get; set; }       // Current page rows
    public int? TotalCount { get; set; }             // Filtered source records
    public int? ResultCount { get; set; }            // Shaped rows before paging
    public int Page { get; set; }                    // Current page number
    public int PageSize { get; set; }                // Items per page
    public Dictionary<string, Dictionary<string, object>>? Aggregates { get; set; }  // Grand totals
}
```

`TotalCount` and `ResultCount` are the same for normal cardinality-preserving queries. They differ when a query changes cardinality, such as `GROUP BY`, `DISTINCT`, or future pivot-shaped queries:

```text
1432 orders
GROUP BY CustomerId

TotalCount  = 1432 source rows
ResultCount = number of customer groups
Data.Count  = groups returned in the current page
```

## Best Practices

1. **The SQL dialect is auto-detected from the `DbConnection`** — no manual dialect configuration is needed
2. **Use `AllowedFields`** — Dapper generates raw SQL; restricting fields is critical for security
3. **Set `CommandTimeoutSeconds` for complex queries** — The default is 30 seconds
4. **Prefer `FlexQueryParameters` overloads** for API endpoints — They handle parsing and validation automatically
5. **Use `ScanEntitiesFromAssembly`** during startup — Avoid lazy mapping discovery in hot paths
6. **Reuse connections** — FlexQuery does not manage connection lifecycle; follow Dapper's connection pooling best practices

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Using an unsupported database provider | Throws `NotSupportedException`. Supported: SQL Server, PostgreSQL, SQLite, MySQL, MariaDB, Oracle |
| Property names don't match column names | Use `Entity<T>().Property()` for explicit mapping |
| N+1 queries with includes | Dapper includes generate JOINs — no N+1, but watch for Cartesian explosion with multiple collections |
| Connection not opened before query | Call `await connection.OpenAsync()` before `FlexQueryAsync` |

## Related Features

- [SQL Generation](/providers/dapper/sql-generation) — How QueryOptions become SQL
- [Dialects](/providers/dapper/dialects) — Database-specific syntax details
- [Conventions](/providers/dapper/conventions) — Table and column mapping
- [Relationship Queries](/providers/dapper/relationship-queries) — EXISTS, JOINs, and includes
