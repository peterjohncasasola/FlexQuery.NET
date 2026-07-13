# FlexQuery.NET.AspNetCore

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.AspNetCore.svg)](https://www.nuget.org/packages/FlexQuery.NET.AspNetCore)

ASP.NET Core integration with declarative field-access security.

## When to Use This Package

Install this package when you want to use `[FieldAccess]` attributes on your API controllers to declare per-endpoint security rules, or when you need automatic model binding for `FlexQueryParameters`.

## Installation

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

## Registration

```csharp
builder.Services.AddControllers()
    .AddFlexQuerySecurity();
```

## Quick Start

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet]
    [FieldAccess(AllowedFields = new[] { "Id", "Name", "Email", "Status" },
                 MaxFieldDepth = 2)]
    public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
    {
        var options = HttpContext.GetFlexQueryExecutionOptions();
        options.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status" };

        var result = await _context.Users.FlexQueryAsync(parameters, options);
        return Ok(result);
    }
}
```

## Features

- **`[FieldAccess]` Attribute** — Declare Allowed, Blocked, Filterable, Sortable, Selectable, Groupable, Aggregatable fields per-endpoint
- **`FieldAccessFilter`** — Action filter that applies attribute settings to `QueryExecutionOptions`
- **`GetFlexQueryExecutionOptions`** — Extension method on `HttpContext` to retrieve `QueryExecutionOptions` populated by the `FieldAccessFilter`
- **Swagger Integration** — Works with Swagger/Swashbuckle for API documentation

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.EntityFrameworkCore](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.EntityFrameworkCore/README.md) — EF Core execution
- [FlexQuery.NET.Dapper](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Dapper/README.md) — Dapper execution

## Documentation

- [ASP.NET Integration Guide](https://flexquery.vercel.app/guide/aspnet-integration)
- [Security & Governance](https://flexquery.vercel.app/guide/security-governance)
- [Swagger Integration](https://flexquery.vercel.app/guide/swagger-integration)
