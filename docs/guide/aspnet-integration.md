# ASP.NET Core Integration

The `FlexQuery.NET.AspNetCore` package provides first-class integration with ASP.NET Core — including action filters, attributes, and middleware helpers.

---

## Installation

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

---

## Service Registration

In `Program.cs`:

```csharp
using FlexQuery.NET.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register FlexQuery.NET ASP.NET Core services
builder.Services.AddFlexQuery();

// Or with EF Core services
builder.Services.AddFlexQuery(options =>
{
    options.DefaultPageSize = 20;
    options.MaxPageSize     = 200;
});

builder.Services.AddControllers();
```

`AddFlexQuery()` registers:

- `FieldAccessFilter` as a global action filter
- `IFieldAccessResolver` if configured
- Required service dependencies

---

## FlexQueryParameters Binding

`FlexQueryParameters` is bound directly from the query string using `[FromQuery]`:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
    });

    return Ok(result);
}
```

All properties on `FlexQueryParameters` are nullable, so they are optional in the query string.

---

## [FieldAccess] Attribute

The `[FieldAccess]` attribute declares field security rules declaratively on controller actions or classes.

```csharp
[FieldAccess(
    Allowed    = ["id", "name", "email", "status", "age", "createdAt"],
    Blocked    = ["passwordHash", "twoFactorSecret"],
    Filterable = ["name", "status", "age"],
    Sortable   = ["name", "createdAt", "age"],
    Selectable = ["id", "name", "email"],
    MaxDepth   = 2
)]
[HttpGet]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters,
    QueryExecutionOptions exec)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, o =>
    {
        o.AllowedFields    = exec.AllowedFields;
        o.BlockedFields    = exec.BlockedFields;
        o.FilterableFields = exec.FilterableFields;
        o.SortableFields   = exec.SortableFields;
        o.SelectableFields = exec.SelectableFields;
        o.MaxFieldDepth    = exec.MaxFieldDepth;
    });

    return Ok(result);
}
```

The `[FieldAccess]` attribute is processed by `FieldAccessFilter` before the action executes. It merges the attribute values into the `QueryExecutionOptions` parameter automatically.

**Action takes priority over controller level:**

```csharp
// Controller-level default
[FieldAccess(Allowed = ["id", "name"])]
public class UsersController : ControllerBase
{
    // Action-level override — takes priority
    [FieldAccess(Allowed = ["id", "name", "email", "salary"])]
    [HttpGet("admin")]
    public async Task<IActionResult> GetUsersAdmin(...) { }
}
```

---

## FieldAccessFilter

`FieldAccessFilter` is an `IActionFilter` that reads the `[FieldAccess]` attribute and populates `QueryExecutionOptions` before the action runs.

### Manual Registration (without AddFlexQuery)

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FieldAccessFilter>();
});
```

### How It Works

1. Finds the `[FieldAccess]` attribute on the action or controller.
2. Looks for a `QueryExecutionOptions` parameter in the action arguments.
3. Merges the attribute's field lists into the parameter.

If there is no `[FieldAccess]` attribute or no `QueryExecutionOptions` parameter, the filter is a no-op.

---

## OpenAPI / Swagger Integration

`FlexQueryParameters` generates correct Swagger UI documentation automatically because it is a plain POCO with standard property types.

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MyAPI", Version = "v1" });
});
```

The Swagger UI will show all `FlexQueryParameters` fields as optional query parameters:

```
filter     (string)
sort       (string)
select     (string)
page       (integer)
pageSize   (integer)
...
```

---

## Error Handling

Handle `QueryValidationException` globally via an exception filter:

```csharp
// Minimal API
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ex is QueryValidationException qve)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                title  = "Query validation failed",
                errors = qve.ValidationResult.Errors
            });
            return;
        }
        ctx.Response.StatusCode = 500;
    });
});
```

Or with MVC exception filter:

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

## Minimal API Support

FlexQuery.NET works with Minimal APIs too:

```csharp
app.MapGet("/api/users", async (
    [AsParameters] FlexQueryParameters parameters,
    AppDbContext db) =>
{
    var result = await db.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
    });

    return Results.Ok(result);
});
```

---

## Full Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) => _context = context;

    /// <summary>Get users with dynamic filtering, sorting, and paging.</summary>
    [HttpGet]
    [FieldAccess(
        Allowed    = ["id", "name", "email", "status", "age", "createdAt"],
        Blocked    = ["passwordHash"],
        Filterable = ["name", "status", "age"],
        Sortable   = ["name", "createdAt"],
        Selectable = ["id", "name", "email"],
        MaxDepth   = 2
    )]
    [ProducesResponseType(typeof(QueryResult<object>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] FlexQueryParameters parameters,
        QueryExecutionOptions exec,
        CancellationToken ct)
    {
        try
        {
            var result = await _context.Users.FlexQueryAsync<User>(parameters, o =>
            {
                o.AllowedFields    = exec.AllowedFields;
                o.BlockedFields    = exec.BlockedFields;
                o.FilterableFields = exec.FilterableFields;
                o.SortableFields   = exec.SortableFields;
                o.SelectableFields = exec.SelectableFields;
                o.MaxFieldDepth    = exec.MaxFieldDepth;
            }, ct);

            return Ok(result);
        }
        catch (QueryValidationException ex)
        {
            return BadRequest(new { errors = ex.ValidationResult.Errors });
        }
    }
}
```
