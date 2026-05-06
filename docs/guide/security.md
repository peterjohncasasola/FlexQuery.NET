# Security & Field Access

FlexQuery.NET has a multi-layered field security system. It validates every field in a query — whether used in a filter, sort, or select — against server-side rules **before** execution.

---

## Overview

The security model works at the `QueryExecutionOptions` level. You declare rules per-endpoint and the validator enforces them.

**Validation order:**

1. Depth check (`MaxFieldDepth`)
2. Custom resolver (`FieldAccessResolver`)
3. Blocked fields (`BlockedFields`)
4. Role-based access (`RoleAllowedFields`)
5. Operation-level rules (`FilterableFields`, `SortableFields`, `SelectableFields`)
6. Global allowed list (`AllowedFields`)

---

## Field Security Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `AllowedFields` | `HashSet<string>?` | Global allow-list. If set, every field must be in this list. |
| `BlockedFields` | `HashSet<string>?` | Fields explicitly denied — always checked, even if in AllowedFields. |
| `FilterableFields` | `HashSet<string>?` | Fields allowed in filter expressions only. |
| `SortableFields` | `HashSet<string>?` | Fields allowed in sort expressions only. |
| `SelectableFields` | `HashSet<string>?` | Fields allowed in select/projection only. |
| `MaxFieldDepth` | `int?` | Maximum dot-notation path depth (e.g., `2` allows `profile.bio` but not `a.b.c`). |
| `RoleAllowedFields` | `Dictionary<string, HashSet<string>>?` | Per-role field allow-lists. |
| `CurrentRole` | `string?` | The current user's role name for role-based checks. |
| `FieldMappings` | `Dictionary<string, string>?` | Alias → real field name mappings. |
| `FieldAccessResolver` | `IFieldAccessResolver?` | Custom programmatic resolver. |
| `StrictFieldValidation` | `bool` | If true, throws on first violation. If false, collects all errors. |

---

## AllowedFields

The global allow-list. If set, **every** field in the query (filter, sort, select) must be in this set.

```csharp
exec.AllowedFields = new HashSet<string>
{
    "id", "name", "email", "status", "createdAt"
};
```

Wildcards are supported:

```csharp
exec.AllowedFields = new HashSet<string>
{
    "id", "name", "profile.*"  // allows profile.bio, profile.avatar, etc.
};
```

---

## BlockedFields

Explicitly blocked fields. Checked regardless of AllowedFields.

```csharp
exec.BlockedFields = new HashSet<string>
{
    "passwordHash", "twoFactorSecret", "internalNotes"
};
```

If a client tries to filter or select a blocked field:

```json
{
  "errors": [
    {
      "message": "Field 'passwordHash' is explicitly blocked.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "passwordHash"
    }
  ]
}
```

---

## Operation-Level Restrictions

Restrict which fields can be used for specific operations:

```csharp
exec.FilterableFields  = new HashSet<string> { "name", "status", "age" };
exec.SortableFields    = new HashSet<string> { "name", "createdAt" };
exec.SelectableFields  = new HashSet<string> { "id", "name", "email" };
```

A field in `AllowedFields` but **not** in `FilterableFields` can be selected but cannot be filtered.

---

## MaxFieldDepth

Prevents deep path traversal attacks.

```csharp
exec.MaxFieldDepth = 2; // allows: name, profile.bio — blocks: profile.address.city
```

If a client sends `profile.address.city.zip` (depth = 4):

```json
{
  "errors": [
    {
      "message": "Field path 'profile.address.city.zip' exceeds maximum allowed depth of 2.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "profile.address.city.zip"
    }
  ]
}
```

---

## Role-Based Access

Restrict fields based on the user's role:

```csharp
exec.RoleAllowedFields = new Dictionary<string, HashSet<string>>
{
    ["admin"]  = new HashSet<string> { "id", "name", "email", "salary", "internalRating" },
    ["viewer"] = new HashSet<string> { "id", "name", "email" }
};

// Set from ClaimsPrincipal
exec.CurrentRole = User.IsInRole("admin") ? "admin" : "viewer";
```

---

## Where to Configure AllowedFields

### Option 1: Controller-Level (Inline)

Best for simple, single-endpoint rules.

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

**Pros:** Explicit, easy to read, no magic.
**Cons:** Duplicated across similar endpoints.

---

### Option 2: [FieldAccess] Attribute

Best for standardizing rules across a controller.

```csharp
[FieldAccess(
    Allowed    = ["id", "name", "email", "status"],
    Blocked    = ["passwordHash"],
    Filterable = ["name", "status"],
    Sortable   = ["name", "createdAt"],
    Selectable = ["id", "name", "email"],
    MaxDepth   = 2
)]
[HttpGet]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters,
    QueryExecutionOptions exec)
{
    // exec is populated automatically by FieldAccessFilter
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

Register `FieldAccessFilter` globally in `Program.cs`:

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FieldAccessFilter>();
});
```

**Pros:** Declarative, DRY, works at class level.
**Cons:** Requires `FieldAccessFilter` registration.

---

### Option 3: Centralized Resolver

Best for dynamic, runtime-computed rules (e.g., based on tenant, claims, database config).

```csharp
public class TenantFieldAccessResolver : IFieldAccessResolver
{
    private readonly ITenantService _tenants;

    public TenantFieldAccessResolver(ITenantService tenants) => _tenants = tenants;

    public bool IsAllowed(string field, QueryOperation operation, QueryContext context)
    {
        var tenantConfig = _tenants.GetConfig(context.TenantId);
        return tenantConfig.AllowedFields.Contains(field);
    }
}

// Register
builder.Services.AddScoped<IFieldAccessResolver, TenantFieldAccessResolver>();
```

```csharp
// In controller
exec.FieldAccessResolver = _resolver;
```

**Pros:** Fully dynamic, database-driven, multi-tenant capable.
**Cons:** More complex to set up.

---

## Comparison: Where to Configure

| Approach | Complexity | Flexibility | DRY | Best For |
| :--- | :--- | :--- | :--- | :--- |
| Inline in controller | Low | Fixed | ❌ | Simple, single endpoints |
| `[FieldAccess]` attribute | Low | Fixed | ✅ | Standard controller-level rules |
| Centralized resolver | High | Dynamic | ✅ | Multi-tenant, role-driven, DB-driven |

---

## Validation Error Response

When validation fails, the response is structured:

```json
{
  "errors": [
    {
      "message": "Field 'salary' is not in the global allowed list.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "salary"
    }
  ]
}
```

Handle it in your controller:

```csharp
try
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec => { ... });
    return Ok(result);
}
catch (QueryValidationException ex)
{
    return BadRequest(new { errors = ex.ValidationResult.Errors });
}
```

Or use the soft-validation approach:

```csharp
var options = QueryOptionsParser.Parse(parameters);
var result = options.ValidateSafe<User>(execOptions);

if (!result.IsValid)
    return BadRequest(new { errors = result.Errors });
```

---

## Best Practices

- **Always set `AllowedFields`** on any endpoint exposed to the public internet.
- **Always set `MaxFieldDepth`** to prevent traversal attacks.
- **Set `BlockedFields`** for sensitive fields even if they are not in `AllowedFields` — defense in depth.
- **Use `StrictFieldValidation = true`** in production to fail fast on the first violation.
- **Never trust client-provided field names** — always validate before execution.
