# FlexQuery.NET

**Dynamic filtering, sorting, paging, and projection for IQueryable in .NET.**

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![Dotnet Support](https://img.shields.io/badge/.NET-6.0%20%7C%208.0%20%7C%2010.0-blueviolet)](https://dotnet.microsoft.com/download)
[![Documentation](https://img.shields.io/badge/docs-vercel-blue.svg)](https://flexquery.vercel.app)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

FlexQuery.NET is a lightweight and powerful dynamic query engine for .NET. It allows you to transform complex API query parameters into optimized, EF Core-translatable expression trees with a single line of code.

### ⚡ Key Features

- **Dynamic Querying**: Powerful DSL, JQL, and JSON-based filtering.
- **IQueryable-Native**: 100% server-side translation—no client-side evaluation.
- **Advanced Projection**: Automatic SQL `SELECT` optimization including nested includes.
- **Governance & Security**: Built-in field-level validation and operator restrictions.
- **High Performance**: Thread-safe expression caching for ultra-low latency.

---

## 🚀 Quick Start

### 1. Installation

```bash
dotnet add package FlexQuery.NET
dotnet add package FlexQuery.NET.EntityFrameworkCore
dotnet add package FlexQuery.NET.Dapper
dotnet add package FlexQuery.NET.AspNetCore
dotnet add package FlexQuery.NET.Adapters.AgGrid
```

### 2. Entity Framework Core (Default)

Securely execute a dynamic query directly from your controller against an EF Core `DbContext`. The provider handles translation, pagination, and async execution automatically.

```csharp
[HttpGet("users")]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, options => 
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status" };
        options.StrictFieldValidation = true;
        options.UseNoTracking = true; // Optimization for read-only queries
    });

    return Ok(result);
}
```

### 3. Dapper & Raw SQL Integration

For high-performance API endpoints or non-EF Core projects, use the Dapper provider to generate secure, dialect-aware, fully parameterized SQL queries.

```csharp
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;

[HttpGet("users")]
public async Task<IActionResult> GetUsersDapper([FromQuery] FlexQueryParameters parameters)
{
    using var connection = new SqlConnection("Server=...;");    
    // Generates parameterized SQL, handles dialects (SQL Server, Postgres, MySQL, etc.)
    var result = await connection.FlexQueryAsync<User>(parameters, options => 
    {
        options.Dialect = new SqlServerDialect(); 
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email" };
    });

    return Ok(result);
}
```

### 4. AG Grid Adapter

`FlexQuery.NET.Adapters.AgGrid` parses AG Grid's Enterprise Server-Side Row Model JSON payloads natively, translating pagination, filtering, sorting, row grouping, and aggregations into FlexQuery operations.

```csharp
[HttpPost("grid")]
public async Task<IActionResult> GetGridData([FromBody] AgGridRequest request)
{
    // 1. Parse AG Grid request into canonical QueryOptions
    var options = request.ToQueryOptions();

    // 2. Execute via EF Core or Dapper
    var result = await _context.Users.FlexQueryAsync<User>(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Status", "CreatedAt" };
    });

    // 3. Return format expected by AG Grid.
    // Grouped SSRM responses should prefer ResultCount when available.
    return Ok(new { rowData = result.Data, rowCount = result.ResultCount ?? result.TotalCount });
}
```

### 5. MiniOData Parser

Migrating from OData? `FlexQuery.NET.Parsers.MiniOData` acts as a drop-in bridge, automatically detecting and parsing OData syntax (`$filter`, `$orderby`, `$top`, `$skip`) on the same endpoint that handles JSON and JQL queries.

```csharp
// Program.cs
builder.Services.AddFlexQueryMiniOData();

// Controller
[HttpGet("products")]
public async Task<IActionResult> GetProducts([FromQuery] FlexQueryParameters parameters)
{
    // Auto-detects OData parameters like:
    // ?$filter=Price gt 50 and Category eq 'Electronics'&$orderby=Name desc
    var result = await _context.Products.FlexQueryAsync(parameters);
    return Ok(result);
}
```

### 6. Example Query Requests

FlexQuery unifies multiple formats under the same API without configuration:

```http
# Native DSL
GET /api/users?filter=age:gt:18&sort=createdAt:desc&page=1&pageSize=20

# JQL Syntax
GET /api/users?filter=Age > 18 AND Status = 'Active'

# OData Syntax (requires MiniOData package)
GET /api/users?$filter=Age gt 18 and Status eq 'Active'
```

## 📚 Documentation

## Counting Semantics

`QueryResult<T>` exposes three different row counts. They answer different questions:

| Property | Meaning |
| ----------- | ------------------------- |
| `TotalCount` | Filtered source records |
| `ResultCount` | Shaped rows before paging |
| `Data.Count` | Current page rows |

For a normal query, `TotalCount` and `ResultCount` usually match:

```text
1432 products
pageSize = 20

TotalCount  = 1432
ResultCount = 1432
Data.Count  = 20
```

For grouped or shaped queries, `ResultCount` is the count most UI grids need for paging:

```text
1432 products
GROUP BY Brand

4 brand groups

TotalCount  = 1432
ResultCount = 4
Data.Count  = current page of groups
```

`HAVING` is applied before `ResultCount`:

```text
1432 products
GROUP BY Brand
HAVING SUM(Quantity) > 100

2 groups remain

TotalCount  = 1432
ResultCount = 2
```

For AG Grid SSRM grouping, prefer:

```csharp
var rowCount = result.ResultCount ?? result.TotalCount;
```

`TotalCount` semantics are unchanged for backward compatibility.

For detailed guides, API references, and advanced scenarios, visit our documentation site:

👉 **[https://flexquery.vercel.app](https://flexquery.vercel.app)**

### Quick Links
- [Getting Started](https://flexquery.vercel.app/guide/getting-started)
- [Query Composition](https://flexquery.vercel.app/guide/composition)
- [Security & Field Access](https://flexquery.vercel.app/guide/security-governance)
- [Dapper Provider](https://flexquery.vercel.app/providers/dapper/getting-started)
- [AG Grid Integration](https://flexquery.vercel.app/adapters/ag-grid)
- [MiniOData Parser](https://flexquery.vercel.app/adapters/miniodata)

---

## 📄 License

FlexQuery.NET is licensed under the [MIT License](LICENSE).
