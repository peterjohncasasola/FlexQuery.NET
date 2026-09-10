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

The SQL dialect is auto-detected from the `DbConnection` at runtime — no manual dialect configuration is required. Register your entity mapping model once at startup:

```csharp
using FlexQuery.NET.Dapper.Configuration;

FlexQueryDapper.Configure(options =>
{
    options.Model.Entity<User>(entity =>
    {
        entity.ToTable("Users");
    });
    options.CommandTimeout = 60;
});
```

Or configure options per query:

```csharp
options.CommandTimeout = 60;
```

## Quick Start

```csharp
using FlexQuery.NET.Models;

[HttpGet("users")]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    await using var connection = new SqlConnection(connectionString);

    var result = await connection.FlexQueryAsync<User>(parameters, options =>
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email" };
    }, cancellationToken: cancellationToken);

    return Ok(result);
}
```

## Features

- **SQL Generation** — `SqlTranslator` produces parameterized, injection-safe SQL
- **Dialect Support** — SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, and Oracle, auto-detected from the connection
- **Flat Projection** — Deep select paths (e.g., `Orders.Total`) become `LEFT JOIN` with flattened aliases
- **Entity Mapping** — Fluent entity-to-table mapping via `ModelBuilder` and `IEntityTypeConfiguration<T>`
- **Filtered Includes** — Related collections hydrated from the include tree
- **SQL Execution Logging** — Execution logs include copy-paste-ready `DECLARE` scripts
- **Typed DTO Results** — `FlexQueryAsync<TEntity, TResponse>` overloads for strongly-typed responses
- **Diagnostics** — Pass a listener via the options to observe pipeline stages

## Known Limitations

- Some aggregate functions may vary by dialect

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core integration
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — Alternative provider for EF Core

## Documentation

- [Dapper Provider Guide](https://flexquery.vercel.app/docs/providers/dapper)
