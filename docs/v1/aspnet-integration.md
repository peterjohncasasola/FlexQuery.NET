> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# ASP.NET Core Integration

FlexQuery.NET provides deep integration with ASP.NET Core, allowing you to secure your dynamic APIs using declarative attributes.

## Installation

Install the ASP.NET Core integration package:

```bash
dotnet add package FlexQuery.NET.AspNetCore
```

## Security Registration

To enable attribute-based security, you must register the security filters in your `Program.cs`.

```csharp
using FlexQuery.NET.AspNetCore.Extensions;

// For MVC/Web API Controllers
builder.Services.AddControllers()
    .AddFlexQuerySecurity();

// OR manual filter registration
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FieldAccessFilter>();
});
```

## The [FieldAccess] Attribute

The `[FieldAccess]` attribute is used to define whitelists and blacklists for dynamic queries at the class or method level.

### Whitelisting Fields

Restrict access to a specific set of fields:

```csharp
[ApiController]
[Route("api/[controller]")]
[FieldAccess(Allowed = new[] { "Id", "Name", "Email" })]
public class UsersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] QueryOptions options)
    {
        return Ok(await _context.Users.ApplyValidatedQueryOptions(options).ToListAsync());
    }
}
```

### Action-Level Overrides

Attributes on actions are **merged** with controller-level attributes. You can use wildcards to allow all sub-properties.

```csharp
[FieldAccess(Allowed = new[] { "Id", "Name" })]
public class CustomersController : ControllerBase
{
    [HttpGet]
    [FieldAccess(Allowed = new[] { "Orders.*" })] // Adds Orders and all sub-fields to the whitelist
    public async Task<IActionResult> Get([FromQuery] QueryOptions options)
    {
        // Allowed: Id, Name, Orders.Id, Orders.Status, etc.
        return Ok(await _context.Customers.ApplyValidatedQueryOptions(options).ToListAsync());
    }
}
```

## Automatic Parameter Binding

When `AddFlexQuerySecurity()` is used, the library automatically injects a `FieldAccessFilter` that:

1.  Identifies any `QueryOptions` or `QueryRequest` parameters in your action.
2.  Resolves all `[FieldAccess]` attributes on the path.
3.  Merges the rules into the `QueryOptions` object before your action code runs.

## Manual Rule Enforcement

If you prefer not to use attributes, you can still use the underlying services to enforce rules based on custom logic (e.g., user roles).

```csharp
public async Task<IActionResult> Get([FromQuery] QueryOptions options)
{
    if (User.IsInRole("Admin"))
    {
        options.AllowedFields = null; // Full access
    }
    else
    {
        options.AllowedFields = new HashSet<string> { "Id", "Name" };
    }
    
    return Ok(await _context.Users.ApplyValidatedQueryOptions(options).ToListAsync());
}
```

