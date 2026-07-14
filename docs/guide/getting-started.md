# Getting Started

## Overview

This guide provides a comprehensive walkthrough for installing and configuring FlexQuery.NET. It takes you from an empty ASP.NET Core project to a fully secured, dynamic API endpoint in under 10 minutes.

## Why this feature exists

Setting up dynamic querying often requires piecing together multiple libraries, writing custom MVC binders, or overriding default Entity Framework behaviors. FlexQuery.NET is designed to be plug-and-play. The built-in integration packages abstract away the complexity of model binding and dependency injection, so you can focus on writing your security policies.

## When to use

- Read this guide when you are setting up FlexQuery.NET in a new project.
- Use the **Manual Pipeline** section when you need fine-grained control over exactly when the query is executed (e.g., if you need to run secondary database checks midway through the execution pipeline).

---

## Installation

FlexQuery.NET is modular. Install only the packages that match your stack:

```bash
# Core library (parsers, AST, validation, FlexQueryParameters)
dotnet add package FlexQuery.NET

# EF Core async execution provider
dotnet add package FlexQuery.NET.EntityFrameworkCore

# ASP.NET Core integration ([FieldAccess] attribute, global exceptions)
dotnet add package FlexQuery.NET.AspNetCore
```

---

## Basic Setup (ASP.NET Core + EF Core)

### Step 1: Configure Services

In `Program.cs`, you must register the core engine and your execution provider.

```csharp
using FlexQuery.NET.DependencyInjection;
using FlexQuery.NET.EntityFrameworkCore.DependencyInjection;
using FlexQuery.NET.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// 1. Register FlexQuery Core globally
builder.Services.AddFlexQuery(options =>
{
    options.MaxPageSize = 1000;
    options.DefaultPageSize = 50;
    options.CaseInsensitive = true;
    options.IncludeTotalCount = true;
    options.StrictFieldValidation = true; // Security: Throws on unauthorized access
    options.MaxFieldDepth = 5;
});

// 2. Register the EF Core Provider
builder.Services.AddFlexQueryEntityFrameworkCore();

// 3. Register MVC and optional declarative Security ([FieldAccess])
builder.Services
    .AddControllers()
    .AddFlexQuerySecurity();

var app = builder.Build();

app.MapControllers();
app.Run();
```

### What does `AddFlexQuerySecurity()` do?

This optional integration automatically registers the `FieldAccessFilter` into the ASP.NET Core MVC pipeline. This enables you to use declarative security attributes directly on your controllers:

```csharp
[FieldAccess(AllowedFields = new[] { "Id", "Name", "Email" })]
[HttpGet]
public async Task<IActionResult> GetUsers() { ... }
```

### Step 2: Define Your Entity

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

## Your First Endpoint (Complete Runnable Example)

This is the recommended production pattern using `FlexQueryAsync`, which automatically handles parsing, validation, and execution in a single line.

```csharp
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var result = await _context.Users.FlexQueryAsync(parameters, exec =>
            {
                // Security: Declare which fields clients are allowed to view/filter/sort
                exec.AllowedFields = new HashSet<string>
                {
                    "Id", "Name", "Email", "Status", "Age", "CreatedAt"
                };

                // Block highly sensitive fields absolutely
                exec.BlockedFields = new HashSet<string> { "PasswordHash" };

                // Limit nesting depth (prevents infinite traversal via includes)
                exec.MaxFieldDepth = 2;
            });

            return Ok(result);
        }
        catch (QueryValidationException ex)
        {
            // Always return 400 Bad Request if the client violates the AllowedFields policy
            return BadRequest(new { errors = ex.ValidationResult.Errors });
        }
    }
}
```

### Sample Request

```http
GET /api/users?filter=Status:eq:active&sort=Name:asc&page=1&pageSize=10&select=Id,Name,Email
```

### Sample Response

```json
{
  "totalCount": 48,
  "resultCount": 48,
  "page": 1,
  "pageSize": 10,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen", "email": "alice@example.com" },
    { "id": 2, "name": "Bob Smith",  "email": "bob@example.com" }
  ],
  "nextCursorToken": null
}
```

---

## Understanding FlexQueryParameters

`FlexQueryParameters` is the public-facing DTO. Bind it directly from the query string in GET requests.

```csharp
public class FlexQueryParameters
{
    public string? Query    { get; set; }  // JQL: query=status="active"
    public string? Filter   { get; set; }  // DSL: filter=status:eq:active
    public string? Sort     { get; set; }  // sort=name:asc,createdAt:desc
    public string? Select   { get; set; }  // select=id,name,email
    public string? Include  { get; set; }  // include=Orders,Profile
    public string? GroupBy  { get; set; }  // groupBy=status
    public string? Having   { get; set; }  // having=count:gt:5
    public int?    Page     { get; set; }  // page=1
    public int?    PageSize { get; set; }  // pageSize=20
    public bool?   IncludeCount { get; set; }  // includeCount=true
    public bool?   Distinct     { get; set; }  // distinct=true
    public string? Mode     { get; set; }  // mode=Flat
    public bool?   UseKeysetPagination { get; set; }
    public string? Cursor   { get; set; }
}
```

**Why `FlexQueryParameters` and not `Request.Query` directly?**

- It is an **OpenAPI-compatible DTO** — Swagger generates proper documentation automatically.
- It is **type-safe** — all values are bound strongly; mitigating generic string injection risks.
- It is **testable** — you can instantiate it directly in unit tests without mocking an `HttpContext`.

---

## How FlexQueryAsync Works Under the Hood

`FlexQueryAsync` is the unified high-level method. It internally manages the entire query lifecycle:

```text
FlexQueryParameters
    → Parse (QueryOptionsParser)
    → Validate (field access, operators, depth against Server Policy)
    → ApplyFilter
    → ApplySort
    → CountAsync (for totalCount)
    → ApplyPaging
    → ApplyFilteredIncludes
    → ApplySelect (if projection requested)
    → ToListAsync
    → QueryResult<object>
```

---

## Manual Pipeline (Mid-Level Control)

When you need custom logic between steps (e.g., executing business rules before pagination), you can manually orchestrate the pipeline instead of using `FlexQueryAsync`:

```csharp
using FlexQuery.NET;
using FlexQuery.NET.EntityFrameworkCore;

[HttpGet("manual")]
public async Task<IActionResult> GetUsersManual([FromQuery] FlexQueryParameters parameters)
{
    // 1. Parse
    var options = parameters.ToQueryOptions();

    // 2. Validate
    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status" }
    };
    options.ValidateOrThrow<User>(execOptions);

    // 3. Start composing the IQueryable
    var query = _context.Users.AsQueryable();
    query = query.ApplyFilter(options);
    query = query.ApplySort(options);

    // 4. Manual intervention: Count the filtered rows *before* paging cuts them off
    var total = await query.CountAsync();

    // 5. Apply pagination and includes
    query = query.ApplyPaging(options);
    query = query.ApplyExpand(options);

    // 6. Project and execute
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
    AllowedFields     = new HashSet<string> { "Name", "Email", "Status" },
    BlockedFields     = new HashSet<string> { "PasswordHash", "InternalNotes" },
    FilterableFields  = new HashSet<string> { "Name", "Status" },
    SortableFields    = new HashSet<string> { "Name", "CreatedAt" },
    SelectableFields  = new HashSet<string> { "Id", "Name", "Email" },
    MaxFieldDepth     = 2
};

options.ValidateOrThrow<User>(execOptions);
```

If you prefer to avoid exceptions for control flow, you can use `ValidateSafe<T>` to return structured errors:

```csharp
var result = options.ValidateSafe<User>(execOptions);

if (!result.IsValid)
{
    return BadRequest(result.Errors);
}
```

---

## Performance & Optimization

For extremely high-traffic APIs using the EF Core provider, you can enable **Expression Caching**. This instructs FlexQuery to cache the compiled LINQ Expression Trees for repeated query shapes, bypassing the CPU overhead of reflection and tree generation on subsequent identical requests.

In `Program.cs` (Global configuration):

```csharp
using FlexQuery.NET.Caching;

// Enable global expression caching
FlexQueryCacheSettings.EnableCache = true;
FlexQueryCacheSettings.MaxCacheSize = 5000;
```

## Best Practices

- **Global Error Handling:** Do not wrap every controller method in a `try/catch`. Instead, register an ASP.NET Core Exception Middleware to globally catch `QueryValidationException` and map it to a `400 Bad Request`.
- **Always Validate:** Even if you use the manual pipeline, never skip the `ValidateOrThrow` step.

## Related Topics

- [Filtering and Sorting](/guide/filtering)
- [Pagination](/guide/paging)
- [Security Governance](/guide/security-governance)
