# Getting Started

This guide will get you from zero to a working, secure API endpoint in under 10 minutes.

---

## Installation

Install the packages that match your stack:

```bash
# Core library (filtering, sorting, paging, projection, validation)
dotnet add package FlexQuery.NET

# EF Core async execution (FlexQueryAsync, ApplyFilteredIncludes)
dotnet add package FlexQuery.NET.EFCore

# ASP.NET Core integration ([FieldAccess] attribute, FieldAccessFilter)
dotnet add package FlexQuery.NET.AspNetCore
```

---

## Basic Setup (ASP.NET Core + EF Core)

FlexQuery.NET itself does **not** require dependency injection or middleware registration.

The core library works directly through:
- `QueryOptionsParser`
- `IQueryable` extension methods
- expression tree generation

However, if you are using the optional ASP.NET Core integration package (`FlexQuery.NET.AspNetCore`), you can register automatic field-level security filters.

---

In `Program.cs`:

```csharp
using FlexQuery.NET.AspNetCore.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")
    ));

// Register MVC + optional FlexQuery security integration
builder.Services
    .AddControllers()
    .AddFlexQuerySecurity();

var app = builder.Build();

app.MapControllers();

app.Run();
```

---

## What does `AddFlexQuerySecurity()` do?

This optional integration automatically registers:

- `FieldAccessFilter`
- attribute-based field-level security
- MVC filter pipeline integration

This enables features such as:

```csharp
[FieldAccess(AllowedFields = new[] { "Id", "Name", "Email" })]
```

on controllers and actions.

---

## When do I need `AddFlexQuerySecurity()`?

You only need it if you use:

- `[FieldAccess]`
- automatic MVC field-level security
- global `FieldAccessFilter` behavior

If you only use:

- `QueryOptionsParser`
- `ApplyQueryOptions`
- `ApplyValidatedQueryOptions`
- `ToProjectedQueryResultAsync`

then **no ASP.NET Core registration is required**.

---

## Minimal Setup (Without ASP.NET Core Integration)

If you only want the core query engine:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")
    ));

builder.Services.AddControllers();
```

That's it — no additional FlexQuery.NET setup is required.

### 2. Your Entity

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "active";
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Order> Orders { get; set; } = new();
}
```

---

## Your First Endpoint

This is the recommended production pattern using `FlexQueryAsync`:

```csharp
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Models;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
    {
        var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
        {
            // Declare which fields clients are allowed to use
            exec.AllowedFields = new HashSet<string>
            {
                "id", "name", "email", "status", "age", "createdAt"
            };

            // Limit nesting depth (prevents deep path traversal)
            exec.MaxFieldDepth = 2;
        });

        return Ok(result);
    }
}
```

### Sample Request

```
GET /api/users?filter=status:eq:active&sort=name:asc&page=1&pageSize=10&select=id,name,email
```

### Sample Response

```json
{
  "data": [
    { "id": 1, "name": "Alice Chen", "email": "alice@example.com" },
    { "id": 2, "name": "Bob Smith",  "email": "bob@example.com" }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 10
}
```

---

## Understanding FlexQueryParameters

`FlexQueryParameters` is the public-facing DTO. Bind it directly from the query string.

```csharp
public sealed class FlexQueryParameters
{
    public string? Query    { get; set; }  // JQL: query=status = "active"
    public string? Filter   { get; set; }  // DSL: filter=status:eq:active
    public string? Sort     { get; set; }  // sort=name:asc,createdAt:desc
    public string? Select   { get; set; }  // select=id,name,email
    public string? Includes { get; set; }  // includes=Orders,Profile
    public string? GroupBy  { get; set; }  // groupBy=status
    public string? Having   { get; set; }  // having=count():gt:5
    public int?    Page     { get; set; }  // page=1
    public int?    PageSize { get; set; }  // pageSize=20
    public bool?   IncludeCount { get; set; }  // includeCount=true
    public bool?   Distinct     { get; set; }  // distinct=true
    public string? Mode     { get; set; }  // mode=flat
}
```

**Why `FlexQueryParameters` and not `Request.Query` directly?**

- It is an **OpenAPI-compatible DTO** — Swagger generates proper documentation.
- It is **type-safe** — all values are strings, ints, or bools; no injection risk.
- It is **easier to test** — create instances directly in unit tests.
- It is **explicit** — all supported parameters are visible in the class definition.

---

## How FlexQueryAsync Works

`FlexQueryAsync` is the unified high-level method. It does everything in one call:

```
FlexQueryParameters
    → Parse (QueryOptionsParser)
    → Validate (field access, operators, depth)
    → ApplyFilter
    → ApplySort
    → CountAsync (for totalCount)
    → ApplyPaging
    → ApplyFilteredIncludes
    → ApplySelect (if projection requested)
    → ToListAsync
    → QueryResult<object>
```

```csharp
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
});
```

---

## Manual Pipeline (Mid-Level Control)

When you need custom logic between steps, use the manual pipeline:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    // 1. Parse
    var options = QueryOptionsParser.Parse(parameters);

    // 2. Validate
    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "id", "name", "email", "status" }
    };
    options.ValidateOrThrow<User>(execOptions);

    // 3. Apply pipeline
    var query = _context.Users.AsQueryable();
    query = query.ApplyFilter(options);
    query = query.ApplySort(options);

    // 4. Count before paging
    var total = await query.CountAsync();

    query = query.ApplyPaging(options);
    query = query.ApplyFilteredIncludes(options);

    // 5. Project and execute
    var data = await query.ApplySelect(options).ToListAsync();

    return Ok(options.BuildQueryResult(data, total));
}
```

---

## Validation Setup

`ValidateOrThrow<T>` runs the full validation pipeline and throws `QueryValidationException` on failure.

```csharp
var execOptions = new QueryExecutionOptions
{
    AllowedFields     = new HashSet<string> { "name", "email", "status" },
    BlockedFields     = new HashSet<string> { "passwordHash", "internalNotes" },
    FilterableFields  = new HashSet<string> { "name", "status" },
    SortableFields    = new HashSet<string> { "name", "createdAt" },
    SelectableFields  = new HashSet<string> { "id", "name", "email" },
    MaxFieldDepth     = 2
};

options.ValidateOrThrow<User>(execOptions);
```

To return structured errors instead of throwing:

```csharp
var result = options.ValidateSafe<User>(execOptions);

if (!result.IsValid)
{
    return BadRequest(result.Errors);
}
```


---

## Recommended Production Setup

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    try
    {
        var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
        {
            exec.AllowedFields    = new HashSet<string> { "id", "name", "email", "status", "age", "createdAt" };
            exec.BlockedFields    = new HashSet<string> { "passwordHash", "twoFactorSecret" };
            exec.SortableFields   = new HashSet<string> { "name", "createdAt", "age" };
            exec.SelectableFields = new HashSet<string> { "id", "name", "email" };
            exec.MaxFieldDepth    = 2;
        });

        return Ok(result);
    }
    catch (QueryValidationException ex)
    {
        return BadRequest(new { errors = ex.ValidationResult.Errors });
    }
}
```

This gives you:

- ✅ Parsing from query string
- ✅ Field-level security validation
- ✅ Safe filter/sort/page execution
- ✅ Optional projection
- ✅ Structured error response on bad input
