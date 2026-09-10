# FlexQuery.NET.AspNetCore

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.AspNetCore.svg)](https://www.nuget.org/packages/FlexQuery.NET.AspNetCore)

ASP.NET Core integration with declarative field-access security.

## When to Use This Package

Install this package when you want to use `[FieldAccess]` attributes on your API controllers to declare per-endpoint security rules, and to shape `QueryResult<T>` JSON serialization so an explicit `select` exposes only the selected fields.

## Installation

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

## Registration

```csharp
// Global FlexQuery configuration (defaults, entity → DTO maps)
builder.Services.AddFlexQuery(options =>
{
    options.MaxPageSize = 100;
    options.StrictFieldValidation = true;
});

// [FieldAccess] attributes + result-shape JSON converter
builder.Services.AddControllers()
    .AddFlexQuerySecurity();
```

Use `AddFlexQueryJson()` instead of `AddFlexQuerySecurity()` if you only want the result-shape JSON converter without the security filter.

## Quick Start

```csharp
using FlexQuery.NET.AspNetCore.Attributes;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet]
    [FieldAccess(Allowed = new[] { "Id", "Name", "Email", "Status" },
                 MaxDepth = 2)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _context.Users
            .FlexQueryAsync(parameters, cancellationToken: cancellationToken);

        return Ok(result);
    }
}
```

The `FieldAccessFilter` action filter applies the attribute's settings to the request's execution options automatically. The effective options can be retrieved inside the action when needed:

```csharp
var options = HttpContext.GetFlexQueryExecutionOptions();
```

## Features

- **`[FieldAccess]` Attribute** — Declare Allowed, Blocked, Filterable, Sortable, Selectable, Groupable, Aggregatable fields, AllowedIncludes, DefaultSortField/Direction, and MaxDepth per controller or action
- **`FieldAccessFilter`** — Action filter that applies attribute settings and stores them in `HttpContext.Items`
- **Result-Shape JSON** — With an explicit `select`, the response contains exactly the selected output fields (under their aliases)
- **Automatic Security Resolution** — `GetFlexQueryExecutionOptions()` extension for reading the effective per-request options

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — EF Core execution
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Dapper execution

## Documentation

- [ASP.NET Core Integration](https://flexquery.vercel.app/docs/integrations/aspnetcore)
- [Security & Governance](https://flexquery.vercel.app/docs/security)
