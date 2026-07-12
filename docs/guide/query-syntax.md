# Query Syntax

## Overview

FlexQuery.NET supports multiple query syntax dialects, allowing clients to express filters, sorts, and projections in the format most natural to their stack. All syntaxes are parsed into a canonical `QueryOptions` AST so downstream processing is format-independent.

### What It Is

The `QuerySyntax` enum and parser registration system enable pluggable parsing. The active syntax is configured globally via `FlexQueryCore.Configure()` and is applied automatically when `FlexQueryParameters` is parsed via `ToQueryOptions()`.

### Why It Exists

Real-world APIs serve diverse clients. A React dashboard might use compact DSL strings, while a legacy enterprise client might speak OData-style `$filter` syntax. Without syntax abstraction, you would need separate endpoints or custom parsing logic for each client type.

### When to Use It

- You have clients that use different query formats
- You are migrating from OData and need backwards compatibility
- You want to explicitly control which parser handles a request
- You are building a custom parser for a proprietary format

### When NOT to Use It

- If all your clients use the same format, the default DSL syntax works without any configuration
- If you are only passing pre-built `QueryOptions` objects programmatically (no string parsing needed)

---

## Architecture

```
FlexQueryCore.Configure()  ← Sets global QuerySyntax
         │
         ▼
FlexQueryParameters        ← Raw string properties (Filter, Sort, Select, etc.)
         │
         ▼
   ToQueryOptions()        ← Uses the configured QuerySyntax parser
         │
         ▼
     QueryOptions          ← Canonical AST used by all downstream processing
```

### Parser Registration

Syntax parsers are registered explicitly at startup. Built-in parsers are part of the Core package. Optional parsers (FQL, MiniOData) require separate packages:

```csharp
// Native DSL — built in, no registration needed (default)
FlexQueryCore.Configure(options =>
{
    options.QuerySyntax = QuerySyntax.NativeDsl;
});

// FQL — requires FlexQuery.NET.Parsers.Fql package
Fql.Register();
FlexQueryCore.Configure(options =>
{
    options.QuerySyntax = QuerySyntax.Fql;
});

// MiniOData — requires FlexQuery.NET.Parsers.MiniOData package
MiniOData.Register();
FlexQueryCore.Configure(options =>
{
    options.QuerySyntax = QuerySyntax.MiniOData;
});
```

---

## Supported Syntax Types

### NativeDsl — Colon-Separated Syntax (Default)

The most compact format. Fields, operators, and values are separated by colons:

```
GET /api/products?filter=ListPrice:gte:1000&sort=Name:asc
GET /api/products?filter=ProductCategory.Name:eq:Bikes
GET /api/customers?filter=LastName:startswith:Smi&select=Id,LastName,Email
```

**Compound (AND — URL-encode `&` as `%26`):**

```
GET /api/products?filter=ListPrice:gte:1000%26ProductCategory.Name:eq:Bikes
```

**Best for:** Internal tools, compact query strings, URL-friendly APIs.

### FQL — SQL-Like Expression Strings

Requires the `FlexQuery.NET.Parsers.Fql` package and `Fql.Register()` at startup. Human-readable expressions using standard operators:

```
GET /api/products?filter=ListPrice >= 1000 AND Category.Name = 'Bikes'
GET /api/customers?filter=LastName CONTAINS 'Smi' OR Email ENDSWITH '@adventure-works.com'
```

**Best for:** Developer tools, debugging, admin panels, human-written queries.

### MiniOData — OData Compatibility

Requires the `FlexQuery.NET.Parsers.MiniOData` package and `MiniOData.Register()` at startup. Standard OData `$filter` and `$orderby` syntax:

```
GET /api/products?$filter=ListPrice ge 1000 and Category/Name eq 'Bikes'
GET /api/products?$orderby=Name desc&$select=Id,Name,ListPrice&$expand=ProductCategory
```

**Best for:** Migrating from OData, enterprise clients that already speak OData.

---

## Syntax Comparison Table

| Feature | NativeDsl | FQL | MiniOData |
|---------|-----------|-----|-----------|
| **Format** | `field:op:value` | `Field OP Value` | `$filter=Field op Value` |
| **Nested logic** | Limited | Full | Full |
| **URL-friendly** | Very | Needs encoding | Good |
| **Human-readable** | Medium | High | High |
| **Package required** | Core | `FlexQuery.NET.Parsers.Fql` | `FlexQuery.NET.Parsers.MiniOData` |
| **Registration** | None (default) | `Fql.Register()` | `MiniOData.Register()` |

---

## Example: Single Endpoint with Explicit Syntax

```csharp
[HttpGet("products")]
public async Task<IActionResult> GetProducts(
    [FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Products.FlexQueryAsync(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string>
        {
            "Id", "Name", "ListPrice", "ProductCategory"
        };
        opts.MaxPageSize = 100;
    });

    return Ok(result);
}
```

This endpoint handles the syntax configured at startup:

```
# NativeDsl (default)
GET /products?filter=ListPrice:gte:1000&sort=Name:asc

# FQL (if registered)
GET /products?filter=ListPrice >= 1000 AND ProductCategory.Name = 'Bikes'

# MiniOData (if registered)
GET /products?$filter=ListPrice ge 1000 and ProductCategory/Name eq 'Bikes'
```

---

## Forcing a Specific Syntax Per-Request

To override the global syntax for a single request, pass `QueryOptions` directly instead of `FlexQueryParameters`:

```csharp
[HttpPost("products/query")]
public async Task<IActionResult> QueryProducts(
    [FromBody] MiniODataRequest request)
{
    var options = request.ToQueryOptions();
    // options is now parsed using MiniOData syntax
    var result = await _context.Products.FlexQueryAsync(options, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "ListPrice" };
    });

    return Ok(result);
}
```

---

## Best Practices

1. **Pick one syntax and standardize** — If all clients are under your control, pick DSL or FQL and document it.
2. **Use MiniOData for migration only** — It exists for backwards compatibility. For new APIs, prefer DSL or FQL.
3. **Validate the output, not the syntax** — Apply `AllowedFields`, `MaxFieldDepth`, and `StrictFieldValidation` on the parsed `QueryOptions`, not on the raw input.
4. **Register parsers explicitly** — Even though some parsers auto-register on assembly load, explicit registration makes the configuration clear.

---

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Sending DSL filter with FQL syntax configured | The parser fails with a `QueryParseException`. Ensure `QuerySyntax` matches the expected input format. |
| Mixing OData `$filter` with DSL `filter` parameter | Use MiniOData syntax exclusively when parsing OData-style requests. Don't mix `$filter` with `filter`. |
| Forgetting to register a parser | FQL and MiniOData must be explicitly registered via `Fql.Register()` / `MiniOData.Register()` before use. |
| Assuming case sensitivity | All operators are case-insensitive. `EQ`, `eq`, and `Eq` are all valid. |

## Related Features

- [Filtering](/guide/filtering) — How parsed filters become LINQ expressions
- [MiniOData Adapter](/adapters/miniodata) — OData compatibility details
- [AG Grid Adapter](/adapters/ag-grid) — Parsing AG Grid JSON payloads
