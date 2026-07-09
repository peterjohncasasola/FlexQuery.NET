# ASP.NET Core Integration

## Overview

The `FlexQuery.NET.AspNetCore` package provides optional integration helpers for ASP.NET Core applications. It bridges the gap between raw HTTP requests and the FlexQuery.NET execution engine, primarily focusing on declarative security, global error handling, and `HttpContext` binding.

## Why this feature exists

While you can manually instantiate `FlexQueryParameters` and pass lambdas to `FlexQueryAsync` everywhere, large MVC applications often prefer convention over configuration. This package provides the `[FieldAccess]` attribute, allowing you to define your security rules directly on your controller methods alongside your HTTP verb attributes, keeping your actions clean and standardized.

## When to use

- You are building an ASP.NET Core MVC or Web API application.
- You want to use declarative `[FieldAccess]` attributes on your controllers instead of configuring security inside lambda expressions in every endpoint.
- You want to globally catch and format query validation errors.

---

## Installation

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

---

## Service Registration

Unlike v3, FlexQuery.NET v4 relies on a robust dependency injection container. To enable the declarative security attributes, register the core engine, your execution provider, and the ASP.NET Core security filters in `Program.cs`:

```csharp
using FlexQuery.NET.DependencyInjection;
using FlexQuery.NET.EntityFrameworkCore.DependencyInjection;
using FlexQuery.NET.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Core Engine
builder.Services.AddFlexQuery();

// 2. Register Execution Provider (e.g. EF Core)
builder.Services.AddFlexQueryEntityFrameworkCore();

// 3. Register MVC with FlexQuery Security
builder.Services.AddControllers()
    .AddFlexQuerySecurity();

var app = builder.Build();
```

> [!NOTE]
> `AddFlexQuerySecurity()` registers the `FieldAccessFilter` into the ASP.NET Core MVC pipeline. It requires the core `AddFlexQuery()` engine to be registered first.

---

## Declarative Security: `[FieldAccess]`

The `[FieldAccess]` attribute allows you to define field security rules directly on your controller actions. It accepts the same properties found on `BaseQueryOptions`.

```csharp
[HttpGet]
[FieldAccess(
    AllowedFields    = new[] { "Id", "Name", "Email", "Status" },
    FilterableFields = new[] { "Name", "Status" },
    SortableFields   = new[] { "Name", "CreatedAt" },
    MaxFieldDepth    = 2
)]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    // The FlexQueryAsync overload natively extracts the security rules 
    // from the HttpContext metadata populated by the [FieldAccess] filter.
    var result = await _context.Users.FlexQueryAsync(parameters, HttpContext);
    
    return Ok(result);
}
```

### How it Works
1. The **`FieldAccessFilter`** intercepts the request.
2. It looks for a **`[FieldAccess]`** attribute on the action or controller metadata.
3. If found, it populates a **`QueryExecutionOptions`** object and stores it in **`HttpContext.Items`**.
4. The **`FlexQueryAsync(..., HttpContext)`** extension method retrieves the options and enforces the declarative policy during query compilation.

---

## Global Exception Handling

FlexQuery.NET halts execution and throws a `QueryValidationException` when a client requests an unauthorized field or violates max depth constraints. You can handle this globally using an Exception Filter or ASP.NET Core Middleware.

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FlexQuery.NET.Exceptions;

public class FlexQueryExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is QueryValidationException ex)
        {
            context.Result = new BadRequestObjectResult(new
            {
                title  = "Query validation failed",
                errors = ex.ValidationResult.Errors
            });
            context.ExceptionHandled = true;
        }
    }
}

// Register globally
builder.Services.AddControllers(o => 
{
    o.Filters.Add<FlexQueryExceptionFilter>();
});
```

---

## OpenAPI / Swagger Integration

`FlexQueryParameters` is a standard POCO that maps naturally to OpenAPI/Swagger documentation generators like Swashbuckle or NSwag.

```csharp
[HttpGet]
public async Task<ActionResult<QueryResult<User>>> GetUsers(
    [FromQuery] FlexQueryParameters parameters)
{
    return await _context.Users.FlexQueryAsync(parameters, HttpContext);
}
```

Swagger UI will automatically display the query string parameters bound to the object:
- `filter`
- `sort`
- `select`
- `page`
- `pageSize`
- `includeCount`
- `mode`

---

## Minimal APIs

The `[FieldAccess]` attribute and `AddFlexQuerySecurity()` pipeline are designed specifically for the MVC/Web API Controller pipeline. For Minimal APIs, we strongly recommend manual configuration using the inline lambda, which is highly performant and explicit:

```csharp
app.MapGet("/api/users", async (
    [AsParameters] FlexQueryParameters parameters,
    AppDbContext db) =>
{
    var result = await db.Users.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = ["Id", "Name", "Email"];
        exec.MaxFieldDepth = 2;
    });

    return Results.Ok(result);
});
```

## Best Practices

- **Mix and Match:** You can apply `[FieldAccess]` at the class level to secure the entire controller, and then apply tighter `[FieldAccess]` bounds on specific `[HttpGet]` actions.
- **Always Catch Validation Exceptions:** Ensure you have registered the `FlexQueryExceptionFilter` (or a similar middleware) so that malicious query probes return `400 Bad Request` instead of crashing the request with an unhandled `500 Internal Server Error`.
