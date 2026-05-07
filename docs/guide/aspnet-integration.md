# ASP.NET Core Integration

The `FlexQuery.NET.AspNetCore` package provides optional integration helpers for ASP.NET Core applications. These are primarily focused on declarative security and automated validation.

---

## Installation

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

---

## Overview

FlexQuery.NET is designed to be **dependency-injection free** by default. You do not need to register any services to use the core library.

The ASP.NET Core integration package provides two primary features:
1. **`FieldAccessFilter`**: An action filter that automatically applies security rules from attributes.
2. **`FlexQueryParameters`**: A unified DTO for query-string binding.

---

## Service Registration

To enable the declarative security attributes, register the security filters in `Program.cs`:

```csharp
using FlexQuery.NET.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// For MVC/Web API Controllers
builder.Services.AddControllers()
    .AddFlexQuerySecurity();

// OR manual filter registration
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FieldAccessFilter>();
});
```

> [!NOTE]
> `AddFlexQuerySecurity()` only registers the `FieldAccessFilter`. It does not provide a full DI framework for the core library, which remains intentionally decoupled from the ASP.NET Core container.

---

## Declarative Security: `[FieldAccess]`

The `[FieldAccess]` attribute allows you to define field security rules directly on your controller actions.

```csharp
[HttpGet]
[FieldAccess(
    Allowed    = ["id", "name", "email", "status"],
    Filterable = ["name", "status"],
    Sortable   = ["name", "createdAt"],
    MaxDepth   = 2
)]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters)
{
    // The FieldAccessFilter automatically populates execution options 
    // into the HttpContext. The FlexQueryAsync overload picks it up.
    var result = await _context.Users.FlexQueryAsync<User>(parameters, HttpContext);
    
    return Ok(result);
}
```

### How it Works
1. The **`FieldAccessFilter`** intercepts the request.
2. It looks for a **`[FieldAccess]`** attribute on the action or controller.
3. If found, it populates a **`QueryExecutionOptions`** object and stores it in **`HttpContext.Items`**.
4. The **`FlexQueryAsync(..., HttpContext)`** extension method retrieves it and enforces the server-owned policy.

---

## Global Exception Handling

You can handle query validation errors globally using an Exception Filter or Middleware.

```csharp
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
builder.Services.AddControllers(o => o.Filters.Add<FlexQueryExceptionFilter>());
```

---

## OpenAPI / Swagger Integration

`FlexQueryParameters` is a standard POCO that maps naturally to OpenAPI/Swagger.

```csharp
[HttpGet]
public async Task<ActionResult<QueryResult<User>>> GetUsers(
    [FromQuery] FlexQueryParameters parameters)
{
    return await _context.Users.FlexQueryAsync<User>(parameters);
}
```

Swagger UI will automatically display the query parameters:
- `filter`
- `sort`
- `select`
- `page`
- `pageSize`
- `includeCount`

---

## Minimal APIs

The security attributes are currently designed for MVC/Web API Controllers. For Minimal APIs, we recommend manual configuration:

```csharp
app.MapGet("/api/users", async (
    [AsParameters] FlexQueryParameters parameters,
    AppDbContext db) =>
{
    var result = await db.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = ["id", "name", "email"];
        exec.MaxFieldDepth = 2;
    });

    return Results.Ok(result);
});
```
