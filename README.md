<p align="center">
  <img src="https://raw.githubusercontent.com/peterjohncasasola/FlexQuery.NET/main/assets/logo.png" alt="FlexQuery.NET Logo" width="400">
</p>

# FlexQuery.NET

**Dynamic filtering, sorting, paging, and projection for IQueryable in .NET.**

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![Dotnet Support](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0-blueviolet)](https://dotnet.microsoft.com/download)
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
- **Explicit Joins**: SQL-like join support with alias-aware field resolution.

---

## 🚀 Quick Start

### 1. Installation

```bash
dotnet add package FlexQuery.NET
dotnet add package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.Dapper
dotnet add package FlexQuery.NET.AspNetCore
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

### 3. Example Request

```http
GET /api/users?filter=age:gt:18&sort=createdAt:desc&page=1&pageSize=20&select=id,name,email
```

### 4. Dapper Integration & Database Dialects

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

---

## 🏎️ Performance Benchmarks

FlexQuery.NET is engineered for performance with transparent, reproducible benchmarks. We measure parsing, expression generation, full end-to-end execution, and API-level latency against Gridify, Sieve, OData, and GraphQL.

> For the complete benchmark suite with methodology, fairness disclaimers, and full analysis, see **[docs/guide/performance/](docs/guide/performance/)**.

### End-to-End Execution (EF Core InMemory, 1,000 records)

*Scenario: Filter (2 conditions) + Sort + Paging (100 items) on 1,000 Users with related Orders and OrderItems.*

| Library | Mean | Relative | Allocated |
|:---------|-----:|---------:|----------:|
| **FlexQuery.NET** | **17.67 ms** | **0.44×** | 21.42 KB |
| **Handwritten LINQ** | 40.21 ms | 1.00× | 97.11 KB |
| Gridify | 40.33 ms | 1.00× | 107.76 KB |
| System.Linq.Dynamic.Core | 40.95 ms | 1.02× | 110.79 KB |
| Sieve | 41.37 ms | 1.03× | 117.67 KB |

FlexQuery.NET is **2.25× faster than handwritten LINQ** in this InMemory scenario, likely due to expression tree optimization and reduced closure allocation. Full analysis: [Execution Benchmarks](docs/guide/performance/execution.md).

---

### Database Execution (SQL Server LocalDB, 100,000 records)

*Scenario: Simple filter (`status:eq:active`) + page (100 items) against SQL Server with no index.*  

⚠️ **Important:** FlexQuery.NET's default configuration (`IncludeCount=true`) executes an additional COUNT query to return total record count, while the handwritten baseline retrieves data only. This benchmark therefore measures **two roundtrips vs one**. When configured fairly, FlexQuery.NET's filtering overhead is ~3–10% (see details).

| Library | Mean | Relative | Allocated | Queries |
|:---------|-----:|---------:|----------:|---------|
| **Handwritten LINQ** (data only) | 336 µs | 1.00× | 111 KB | 1 SELECT |
| **FlexQuery.NET (with count)** | 20,798 µs | 61.8× | 129 KB | SELECT + COUNT |

The apparent 62× overhead is the cost of the extra COUNT query. Full analysis, fair comparison methodology, and configuration options: [Database Execution](docs/guide/performance/database-execution.md).

---

### API End-to-End (Full ASP.NET Core Pipeline, 100,000 records)

*Scenario: HTTP request with filter + sort + paging + projection, including JSON serialization.*

| Library | PageSize=20 | PageSize=100 | PageSize=100K |
|:---------|------------:|-------------:|--------------:|
| **FlexQuery.NET** | 1.49 ms | 1.64 ms | 2.26 ms |
| GraphQL | 0.90 ms | 0.90 ms | FAILED |
| OData | 1.64 ms | 1.72 ms | 2.24 ms |
| Gridify | 1.56 ms | 1.90 ms | 1.90 ms |
| Sieve | 1.59 ms | 1.97 ms | 1.86 ms |
| Manual LINQ | 1.63 ms | 1.97 ms | 1.89 ms |

Full results with fairness notes: [API Benchmarks](docs/guide/performance/api-benchmarks.md).

---

## 📚 Full Documentation

For detailed methodology, dataset description, reproducibility instructions, and fairness disclaimers:

👉 **[Performance Documentation Index](docs/guide/performance/)**

- [Methodology & Reproducibility](docs/guide/performance/methodology.md)
- [Parsing Benchmarks](docs/guide/performance/parsing.md)
- [Expression Generation](docs/guide/performance/expression-generation.md)
- [End-to-End Execution](docs/guide/performance/execution.md)
- [Database Execution (SQL Server)](docs/guide/performance/database-execution.md)
- [API Benchmarks (vs OData/GraphQL)](docs/guide/performance/api-benchmarks.md)
- [Scalability Analysis](docs/guide/performance/scalability.md)
- [Fairness Disclaimers](docs/guide/performance/fairness-disclaimers.md)
- [Interpretation Guide](docs/guide/performance/interpretation-guide.md)

---

## 📚 Documentation

For detailed guides, API references, and advanced scenarios, visit our documentation site:

👉 **[https://flexquery.vercel.app](https://flexquery.vercel.app)**

### Quick Links
- [Getting Started](https://flexquery.vercel.app/guide/getting-started)
- [Query Composition](https://flexquery.vercel.app/guide/composition)
- [Explicit Joins](https://flexquery.vercel.app/guide/joins)
- [Governance & Security](https://flexquery.vercel.app/guide/security)
- [Performance Optimization](https://flexquery.vercel.app/guide/performance-tuning)
- [Migration Guide (v1 → v2)](https://flexquery.vercel.app/migration/v1-to-v2)

---

## 📄 License

FlexQuery.NET is licensed under the [MIT License](LICENSE).
