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
dotnet add package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.Dapper
dotnet add package FlexQuery.NET.AspNetCore
dotnet add package FlexQuery.NET.AgGrid
```

### 2. Simple Usage

Securely execute a dynamic query directly from your controller:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    // One-stop shop: Parsing + Validation + Execution
    var result = await _context.Users.FlexQueryAsync(parameters, options => 
    {
        options.AllowedFields = ["Id", "Name", "Email", "Status"];
        options.AllowOperators("Status", FilterOperators.Eq, FilterOperators.In);
    });

    return Ok(result);
}
```

### 3. AG Grid Adapter

`FlexQuery.NET.AgGrid` translates AG Grid `filterModel` and `sortModel` payloads into canonical `QueryOptions`, then validates and executes them through the same pipeline as the standard FlexQuery APIs.

```csharp
using FlexQuery.NET.AgGrid;
using FlexQuery.NET.AgGrid.Models;

[HttpPost]
public async Task<IActionResult> GetUsers([FromBody] AgGridRequest request)
{
    var queryOptions = AgGridQueryOptionsParser.Parse(request);

    var result = await _context.Users.FlexQueryAsync(queryOptions, options =>
    {
        options.AllowedFields = ["Id", "Name", "Email", "Status", "CreatedAt"];
        options.AllowOperators("Status", FilterOperators.Eq, FilterOperators.In);
    });

    return Ok(result);
}
```

For advanced scenarios where you need to parse before executing, the adapter parser is public and mirrors the documented manual pipeline used by `QueryOptionsParser`:

```csharp
using FlexQuery.NET.AgGrid.Parsers;

var options = AgGridQueryOptionsParser.Parse(request);

options.ValidateOrThrow<User>(execOptions);

query = query.ApplyFilter(options);
query = query.ApplySort(options);
query = query.ApplyPaging(options);

var data = await query.ApplySelect(options).ToListAsync();
```

### 4. Example Request

```http
GET /api/users?filter=age:gt:18&sort=createdAt:desc&page=1&pageSize=20&select=id,name,email
```

### 5. Dapper Integration & Database Dialects

FlexQuery.NET provides a robust Dapper extension (`FlexQuery.NET.Dapper`) that compiles queries into secure, parameterized, and database-specific SQL.

#### Automatic Dialect Resolution

By default, the SQL dialect is automatically resolved from your database connection (e.g., `SqlConnection` -> `SqlServerDialect`, `NpgsqlConnection` -> `PostgreSqlDialect`).

```csharp
[HttpGet]
public async Task<IActionResult> GetUsersDapper([FromQuery] FlexQueryParameters parameters)
{
    // The dialect is automatically resolved based on the provided NpgsqlConnection
    using var connection = new NpgsqlConnection("Host=localhost;Database=mydb;");
    
    var result = await connection.FlexQueryAsync<UserDto>(parameters, options => 
    {
        options.AllowedFields = ["Id", "Name", "Email"];
        // Dapper specific options
        options.CommandTimeoutSeconds = 60;
    });

    return Ok(result);
}
```

#### Explicit Dialect Configuration

If you need to force a specific SQL dialect for a single query, you can configure it directly:

```csharp
using FlexQuery.NET.Dapper.Dialects;

var result = await connection.FlexQueryAsync<UserDto>(parameters, options => 
{
    // Explicitly configure the dialect for this specific query
    options.Dialect = new MySqlDialect(); 
    // Supported dialects: SqlServerDialect, PostgreSqlDialect, MySqlDialect, MariaDbDialect, SqliteDialect, OracleDialect
});
```

#### Global Dialect Configuration (Optional)

If your entire application uses a single database type and you want to bypass the automatic resolution entirely, you can configure a global default dialect once at startup:

```csharp
// Program.cs or Startup.cs
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;

// Set the global dialect once for the entire application
DapperQueryOptions.GlobalDefaultDialect = new PostgreSqlDialect();

// Or, provide your own custom resolver logic:
// DapperQueryOptions.GlobalDialectResolver = new MyCustomResolver();
```

## 📚 Documentation

For detailed guides, API references, and advanced scenarios, visit our documentation site:

👉 **[https://flexquery.vercel.app](https://flexquery.vercel.app)**

### Quick Links
- [Getting Started](https://flexquery.vercel.app/guide/getting-started)
- [Query Composition](https://flexquery.vercel.app/guide/composition)
- [Governance & Security](https://flexquery.vercel.app/guide/security)
- [Performance Optimization](https://flexquery.vercel.app/guide/performance-tuning)
- [Migration Guide (v1 → v2)](https://flexquery.vercel.app/migration/v1-to-v2)

---

## 📄 License

FlexQuery.NET is licensed under the [MIT License](LICENSE).
