# FlexQuery.NET.EntityFrameworkCore

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.EntityFrameworkCore.svg)](https://www.nuget.org/packages/FlexQuery.NET.EntityFrameworkCore)

Async execution, filtered includes, and projection for EF Core.

## When to Use This Package

Install this package when your data access layer uses Entity Framework Core's `DbContext`. It enables the `FlexQueryAsync` extension methods that execute the full query pipeline — parse, validate, filter, sort, page, project — in a single async call against your `DbSet<T>`.

## Installation

```bash
dotnet add package FlexQuery.NET.EntityFrameworkCore
```


## Quick Start

```csharp
using FlexQuery.NET.EntityFrameworkCore;

[HttpGet("users")]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, options =>
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status" };
        options.StrictFieldValidation = true;
    });

    return Ok(result);
}
```

## Features

- **FlexQueryAsync** — Unified parse-validate-execute pipeline with `EfCoreQueryOptions`
- **Expanded Includes** — Include related collections with inline WHERE filters via `ApplyExpand<T>()`
- **Projection** — Nested, Flat, and FlatMixed projection modes
- **Keyset Pagination** — High-performance cursor-based pagination
- **Execution Options** — `UseNoTracking` for read-only queries
- **Static Configuration** — Configure via `FlexQueryEFCore.Configure()` or `FlexQueryEFCore.Setup()` at startup


## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core integration
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Alternative provider for Dapper

## Documentation

- [EF Core Provider Guide](https://flexquery.vercel.app/providers/ef-core)
- [Filtered Includes](https://flexquery.vercel.app/guide/include-filtering)
- [Projection](https://flexquery.vercel.app/guide/projection)
