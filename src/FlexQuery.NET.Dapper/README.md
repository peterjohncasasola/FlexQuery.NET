# FlexQuery.NET.Dapper

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Dapper.svg)](https://www.nuget.org/packages/FlexQuery.NET.Dapper)

SQL generation and async execution for Dapper.

## When to Use This Package

Install this package when your application uses Dapper instead of Entity Framework Core. FlexQuery.NET.Dapper translates `QueryOptions` into parameterized, dialect-aware SQL and executes it directly via `DbConnection`.

## Installation

```bash
dotnet add package FlexQuery.NET.Dapper
```

## Registration

```csharp
using FlexQuery.NET.Dapper;

FlexQueryDapper.Configure(options =>
{
    options.CommandTimeout = 60;
});
```

Or configure options per-query:

```csharp
options.CommandTimeout = 60;
```

## Quick Start

## Quick Start

```csharp
using FlexQuery.NET.Dapper;

[HttpGet("users")]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    await using var connection = new SqlConnection(connectionString);

    var result = await connection.FlexQueryAsync<User>(parameters, options =>
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email" };
    });

    return Ok(result);
}
```

## Features

- **SQL Generation** — `SqlTranslator` produces parameterized, injection-safe SQL
- **Dialect Support** — SQL Server, PostgreSQL, MySQL, SQLite, MariaDB, Oracle via `ISqlDialect`
- **Auto-Dialect Detection** — Dialect is resolved automatically from the `DbConnection` type at runtime
- **Flat Projection** — Deep select paths (e.g., `Orders.Total`) become `LEFT JOIN` with flattened aliases
- **Entity Mapping** — Fluent entity-to-table mapping via `DapperQueryOptions.Entity<T>()`

## Known Limitations

- Filtered includes (EF Core navigation property expansion) are not supported
- Navigation property projection requires flat projection mode
- Some aggregate functions may vary by dialect

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core integration
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — Alternative provider for EF Core

## Documentation

- [Dapper Provider Guide](https://flexquery.vercel.app/providers/dapper/getting-started)
- [SQL Generation](https://flexquery.vercel.app/providers/dapper/sql-generation)
- [Dialects](https://flexquery.vercel.app/providers/dapper/dialects)
- [Relationship Queries](https://flexquery.vercel.app/providers/dapper/relationship-queries)
