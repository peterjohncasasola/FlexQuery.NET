# MiniOData Parser

## Overview

`FlexQuery.NET.Parsers.MiniOData` is an optional parser package that enables FlexQuery to understand and parse OData-style `$filter`, `$orderby`, `$select`, and `$expand` query parameters. It acts as a bridge for applications migrating from OData to FlexQuery, or for APIs that need to support legacy OData clients alongside modern query formats.

### What It Is

MiniOData is a lightweight OData syntax parser package — not a full OData server. It translates OData query parameter conventions into FlexQuery's canonical `QueryOptions` AST. It does **not** implement OData metadata endpoints (`$metadata`), batch operations, or OData-specific response formatting.

### Why It Exists

Many enterprise applications have existing clients that send OData-style queries. Ripping out OData support and rewriting all client code is expensive and risky. MiniOData lets you drop in FlexQuery behind an OData-compatible API surface without changing client code.

### When to Use It

- You are migrating from Microsoft.AspNetCore.OData or similar OData libraries
- You have existing clients that send `$filter`, `$orderby`, `$top`, `$skip` parameters
- You want to accept OData syntax on the same endpoint that handles JSON and FQL queries

### When NOT to Use It

- You are building a new API from scratch — use FlexQuery's native JSON or FQL syntax instead
- You need full OData compliance (metadata, batch, delta, etc.) — use the official OData library
- Your clients can be updated to use FlexQuery's native formats

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.MiniOData
```

## Registration

```csharp
using FlexQuery.NET.Parsers.MiniOData;

MiniOData.Register();
```

This registers the `MiniODataQueryParser` with the `QueryOptionsParser` and enables the MiniOData syntax. Must be called **once** at startup.

**Important:** If the MiniOData assembly is loaded, the parser is also auto-registered via reflection in the `QueryOptionsParser` static constructor. Explicit registration via `MiniOData.Register()` is recommended for clarity.

## Supported Operators

### Comparison Operators

| OData Operator | Meaning | Example |
|---------------|---------|---------|
| `eq` | Equal | `$filter=Name eq 'John'` |
| `ne` | Not equal | `$filter=Status ne 'Inactive'` |
| `gt` | Greater than | `$filter=Age gt 18` |
| `ge` | Greater than or equal | `$filter=Price ge 9.99` |
| `lt` | Less than | `$filter=Stock lt 10` |
| `le` | Less than or equal | `$filter=Rating le 5` |

### Logical Operators

| OData Operator | Meaning | Example |
|---------------|---------|---------|
| `and` | Logical AND | `$filter=Age gt 18 and Status eq 'Active'` |
| `or` | Logical OR | `$filter=Role eq 'Admin' or Role eq 'Manager'` |

### String Functions

| OData Function | Meaning | Example |
|---------------|---------|---------|
| `contains()` | String contains | `$filter=contains(Name, 'john')` |

## Supported Query Parameters

| OData Parameter | FlexQuery Equivalent | Example |
|----------------|---------------------|---------|
| `$filter` | `Filter` | `$filter=Age gt 18` |
| `$orderby` | `Sort` | `$orderby=Name desc` |
| `$select` | `Select` | `$select=Id,Name,Email` |
| `$expand` | `Include` | `$expand=Orders` |
| `$top` | `PageSize` | `$top=20` |
| `$skip` | Calculated offset | `$skip=40` (with `$top=20` → Page 3) |
| `$count` | `IncludeCount` | `$count=true` |

## Unsupported OData Features

The following OData features are **not** supported by MiniOData:

| Feature | Status | Workaround |
|---------|--------|------------|
| `$metadata` endpoint | ❌ Not supported | Use Swagger/OpenAPI for API discovery |
| `$batch` operations | ❌ Not supported | Use separate API calls |
| `$apply` (aggregation) | ❌ Not supported | Use FlexQuery's native aggregation syntax |
| `not` logical operator | ❌ Not supported | Restructure the filter condition |
| Lambda operators (`any`, `all`) | ❌ Not supported | Use FlexQuery's native include filtering |
| `$compute` | ❌ Not supported | Use `MapField()` for computed fields |
| `$search` | ❌ Not supported | Use FlexQuery's `Query` parameter |
| Type casting (`cast()`) | ❌ Not supported | — |
| Geography/Geometry functions | ❌ Not supported | — |

## Auto-Detection Behavior

You do **not** need to explicitly tell FlexQuery to use the MiniOData parser. The `QueryOptionsParser` auto-detects OData syntax using two signals:

1. **`$` prefix on parameter keys** — If any raw query parameter starts with `$` (e.g., `$filter`, `$orderby`), the MiniOData parser claims the request
2. **OData operator keywords** — If the filter string contains `eq`, `ne`, or `contains(`, the parser recognizes it as OData syntax

This means your API can seamlessly accept JSON DSL, FQL, and OData syntax on the **same endpoint** without structural changes.

## Basic Example

In v4, MiniOData provides a dedicated `MiniODataRequest` canonical DTO that explicitly accepts OData parameters. This separates transport from parsing.

```csharp
[HttpGet("products")]
public async Task<IActionResult> GetProducts(
    [FromQuery] MiniODataRequest request)
{
    // Convert the OData request into standard FlexQuery options
    var options = request.ToQueryOptions();

    var result = await _context.Products.FlexQueryAsync(options);
    return Ok(result);
}
```

```
GET /products?$filter=Price gt 50 and Category eq 'Electronics'&$orderby=Name desc&$top=20
```

## Real-World Example: Migration from OData

### Before (Microsoft.AspNetCore.OData)

```csharp
[EnableQuery]
public IQueryable<Product> Get() => _context.Products;
```

### After (FlexQuery + MiniOData)

```csharp
[HttpGet]
public async Task<IActionResult> Get(
    [FromQuery] MiniODataRequest request)
{
    var options = request.ToQueryOptions();
    var result = await _context.Products.FlexQueryAsync(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
        opts.StrictFieldValidation = true;
        opts.MaxPageSize = 100;
    });

    return Ok(result);
}
```

**What you gain:**
- Field-level security (`AllowedFields`, `BlockedFields`)
- Consistent validation across all syntax types
- No dependency on the full OData model builder
- Same endpoint handles OData + JSON + FQL clients simultaneously

**What you lose:**
- `$metadata` endpoint (replace with Swagger)
- OData response formatting (`@odata.context`, `@odata.count`)
- Full OData compliance certification

## Performance Considerations

- The MiniOData parser has negligible overhead — it performs simple string tokenization
- Parser results are cached by `QueryOptionsParser` using a composite cache key that includes the syntax type
- Auto-detection adds a single string scan per request (checking for `$` prefix)

## Security Considerations

- OData clients can send arbitrary `$filter` expressions. **Always** configure `AllowedFields` and `StrictFieldValidation` to prevent field enumeration
- The `$expand` parameter maps to FlexQuery includes — validate with `AllowedIncludes` to prevent unauthorized data loading
- `$top` without `MaxPageSize` allows clients to request unlimited rows. Always set a `MaxPageSize`

## Best Practices

1. **Use MiniOData as a migration bridge** — For new APIs, prefer FlexQuery's native JSON or FQL syntax
2. **Document supported features** — Clearly communicate to clients which OData features are and aren't supported
3. **Set `AllowedFields`** — OData clients often expect full access to the entity model. Restrict field access explicitly
4. **Test with real OData clients** — Libraries like `Simple.OData.Client` can verify compatibility
5. **Plan your migration** — Gradually move clients from OData to FlexQuery's native syntax, then consider removing the MiniOData package

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Mixing `$filter` and `filter` in the same request | The parser detects `$` prefix and routes to MiniOData. Don't mix conventions |
| Expecting full OData response format | FlexQuery returns `QueryResult<T>`, not OData `@odata.context` responses. You may need a response wrapper |
| Using `not` operator | Not supported. Restructure as a positive condition with the opposite operator |
| Assuming `$expand` loads nested collections | It maps to `Includes`, which works in EF Core but generates JOINs in Dapper |

## Related Features

- [Query Syntax](/guide/query-syntax) — How auto-detection chooses between parsers
- [Security & Governance](/guide/security-governance) — Protecting OData endpoints
- [Filtering](/guide/filtering) — FlexQuery's full filter operator reference
