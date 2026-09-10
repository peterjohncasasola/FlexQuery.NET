# FlexQuery.NET.EntityFrameworkCore

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.EntityFrameworkCore.svg)](https://www.nuget.org/packages/FlexQuery.NET.EntityFrameworkCore)

Async execution, filtered includes, and typed DTO projection for EF Core.

## When to Use This Package

Install this package when your data access layer uses Entity Framework Core's `DbContext`. It enables the `FlexQueryAsync` extension methods that execute the full query pipeline — parse, validate, filter, sort, page, project — in a single async call against your `DbSet<T>`.

## Installation

```bash
dotnet add package FlexQuery.NET.EntityFrameworkCore
```

## Registration

Call once at startup, before any query executes:

```csharp
using FlexQuery.NET.EntityFrameworkCore;

FlexQueryEFCore.Setup();   // registers EF Core-specific operators

// Optional: global EF Core defaults (immutable after the first call)
FlexQueryEFCore.Configure(options =>
{
    options.UseNoTracking = true;
});
```

## Quick Start

```csharp
using FlexQuery.NET.Models;

[HttpGet("users")]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters,
    CancellationToken cancellationToken)
{
    var result = await _context.Users.FlexQueryAsync(parameters, options =>
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status" };
        options.StrictFieldValidation = true;
    }, cancellationToken: cancellationToken);

    return Ok(result);
}
```

## Features

- **`FlexQueryAsync`** — Unified parse-validate-execute pipeline with configurable `EfCoreQueryOptions`
- **Typed DTO Projection** — `FlexQueryAsync<TEntity, TResponse>` returns strongly-typed results, with convention-based mapping (`CreateMap`, `ForMember`, `ForNavigation`)
- **Expand** — Translate `QueryOptions.Expand` into EF Core `Include`/`ThenInclude` chains, each optionally filtered by an inline `Where` clause, via `ApplyExpand<T>()`
- **Projection** — Nested, Flat, and FlatMixed projection modes
- **Execution Options** — `UseNoTracking` for read-only queries (enabled by default)
- **SQL Preview** — `ToSqlPreview()` to inspect the generated SQL without executing
- **Diagnostics** — Pass an `IFlexQueryExecutionListener` via the options to observe pipeline stages
- **EF Core Operators** — `UseEfCoreOperators()` to register the `LIKE` operator handler for EF Core translation

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.AspNetCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.AspNetCore/README.md) — ASP.NET Core integration
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Alternative provider for Dapper

## Documentation

- [EF Core Provider](https://flexquery.vercel.app/docs/providers/ef-core)
- [Include](https://flexquery.vercel.app/docs/guides/include)
- [Projection](https://flexquery.vercel.app/docs/guides/projection)
