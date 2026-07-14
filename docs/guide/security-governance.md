# Security & Governance

## Overview

FlexQuery's security model is designed for APIs that expose dynamic querying to external clients. Every query parameter — field names, sort columns, projection fields, navigation properties — is a potential attack surface. The governance system provides layered defenses.

### Design Philosophy

FlexQuery follows the **principle of least privilege**: if you configure nothing, all fields are accessible. The moment you set `AllowedFields`, only those fields are permitted.

## Field Access Controls

### AllowedFields (Whitelist)

```csharp
opts.AllowedFields = new HashSet<string> { "Id", "Name", "Email", "CreatedAt" };
```

Any field not in the set is rejected (strict mode) or silently removed (permissive mode).

### BlockedFields (Blacklist)

```csharp
opts.BlockedFields = new HashSet<string> { "PasswordHash", "InternalCost", "TenantId" };
```

Evaluated even if `AllowedFields` is not set. Block specific sensitive fields without enumerating every allowed field.

### Default Projection Injection

When no `Select` is specified, `DefaultProjectionRule` auto-injects a governed default projection using the first available source:

```csharp
// Priority: SelectableFields > RoleAllowedFields > AllowedFields > entity props minus BlockedFields
opts.SelectableFields = new HashSet<string> { "Id", "Name", "Email" };
opts.AllowedFields = new HashSet<string> { "Id", "Name" };
// Result: default Select = { "Id", "Name", "Email" }
```

This ensures unprojected queries (e.g., `GET /products` without `$select`) respect field governance instead of returning all entity fields.

### Wildcard Expansion

Wildcard patterns in governance lists are expanded against entity metadata at injection time:

```csharp
opts.SelectableFields = new HashSet<string> { "Id", "Name", "Orders.*" };
// Expands to: { "Id", "Name", "Orders.OrderId", "Orders.Total", "Orders.Status", ... }
```

### Governance Configuration Validation

`GovernanceValidator.ValidateConfiguration()` detects inconsistent governance settings at startup:

```csharp
// Throws: field "SSN" is both blocked and allowed
opts.BlockedFields = new HashSet<string> { "SSN" };
opts.AllowedFields = new HashSet<string> { "Id", "Name", "SSN" };
GovernanceValidator.ValidateConfiguration(opts);
```

Checks enforced:
- `BlockedFields` must not intersect with `AllowedFields`
- `SelectableFields`, `FilterableFields`, `SortableFields`, `GroupableFields`, `AggregatableFields` must each be subsets of `AllowedFields` (when both are configured)

### Per-Operation Restrictions

```csharp
opts.FilterableFields = new HashSet<string> { "Status", "CreatedAt", "Category" };
opts.SortableFields = new HashSet<string> { "Name", "CreatedAt", "Price" };
opts.SelectableFields = new HashSet<string> { "Id", "Name", "Email" };
```

A field can be selectable but not filterable (e.g., `Description` appears in projections but not WHERE clauses).

### AllowedIncludes

```csharp
opts.AllowedIncludes = new HashSet<string> { "Orders", "Profile" };
```

Prevents loading expensive or sensitive relationships like `AuditLogs`.

### AllowedOperators

```csharp
opts.AllowOperators("Email", FilterOperators.Equal, FilterOperators.Contains);
// Blocks: gt, lt, startswith, in, between on Email
```

### MaxFieldDepth

```csharp
opts.MaxFieldDepth = 3; // "Customer.Orders.Items" OK, deeper paths blocked
```

### StrictFieldValidation

```csharp
opts.StrictFieldValidation = true;  // Throws exception for unauthorized fields
opts.StrictFieldValidation = false; // Silently removes unauthorized fields
```

**Strict mode** for public APIs. **Permissive mode** for internal APIs or migration scenarios.

## FlexQueryOptions (Global Defaults)

```csharp
builder.Services.AddFlexQuery(opts =>
{
    opts.MaxPageSize = 1000;
    opts.DefaultPageSize = 20;
    opts.MaxFieldDepth = 5;
    opts.StrictFieldValidation = true;
    opts.UseNoTracking = true;
});
```

## Advanced Security

### Role-Based Field Access

```csharp
opts.RoleAllowedFields = new Dictionary<string, HashSet<string>>
{
    ["admin"] = new() { "Id", "Name", "Email", "Salary", "InternalNotes" },
    ["user"] = new() { "Id", "Name", "Email" },
    ["guest"] = new() { "Id", "Name" }
};
opts.CurrentRole = User.GetRole();
```

### Dynamic Field Resolution

```csharp
opts.AllowedFieldsResolver = entityType =>
{
    if (entityType == typeof(Employee))
        return isHr ? new[] { "Id", "Name", "Salary" } : new[] { "Id", "Name" };
    return null;
};
```

### Custom IFieldAccessResolver

```csharp
opts.FieldAccessResolver = new TenantAwareFieldResolver(tenantConfig);
```

## Real-World Example

```csharp
[HttpGet("products")]
public async Task<IActionResult> GetProducts([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Products.FlexQueryAsync(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string>
            { "Id", "Name", "Description", "Price", "Category", "Rating", "InStock" };
        opts.BlockedFields = new HashSet<string>
            { "CostPrice", "SupplierId", "InternalSku", "Margin" };
        opts.SortableFields = new HashSet<string> { "Name", "Price", "Rating" };
        opts.AllowOperators("Name", "eq", "contains", "startswith");
        opts.AllowedIncludes = new HashSet<string> { "Reviews", "Images" };
        opts.MaxFieldDepth = 2;
        opts.MaxPageSize = 100;
        opts.StrictFieldValidation = true;
    });
    return Ok(result);
}
```

## Security Threat Model

| Threat | Control | Example |
|--------|---------|---------|
| Schema enumeration | `StrictFieldValidation = false` | Don't reveal field names in errors |
| Sensitive data exposure | `BlockedFields` | Block `PasswordHash`, `ApiKey` |
| Expensive queries | `SortableFields`, `MaxFieldDepth` | Prevent unindexed sorts, deep JOINs |
| Cartesian explosion | `AllowedIncludes`, `MaxFieldDepth` | Restrict relationship loading |
| Denial of service | `MaxPageSize` | Cap result set size |
| Operator abuse | `AllowedOperators` | Block `LIKE '%...'` on large text columns |
| Cross-tenant access | Pre-filter + `BlockedFields` | Tenant filter before FlexQuery; block `TenantId` |

## Best Practices

1. **Always set `AllowedFields`** for public APIs
2. **Use `BlockedFields` as defense-in-depth** alongside whitelists
3. **Set `MaxPageSize`** on every endpoint
4. **Restrict `AllowedIncludes`** to prevent expensive relationship loading
5. **Apply tenant isolation before FlexQuery** — `.Where(x => x.TenantId == tid)` must come first
6. **Use strict mode for public APIs, permissive mode for internal APIs**

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Setting `AllowedFields` but forgetting `MapField` aliases | Add aliases to `AllowedFields` |
| Using `AllowedFields` without `AllowedIncludes` | Clients can still load any navigation property |
| Blocking `TenantId` without pre-filtering | `BlockedFields` prevents querying but doesn't filter data |

## Related Features

- [Validation](/guide/validation) — How validation rules are applied
- [Field Mapping](/guide/field-mapping) — How `MapField` interacts with security
- [Filtering](/guide/filtering) — How filter expressions are validated
