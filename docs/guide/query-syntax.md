# Query Syntax

## Overview

FlexQuery supports multiple query syntax dialects, allowing clients to express filters, sorts, and projections in the format most natural to their stack. Whether your frontend team prefers JSON payloads, your legacy clients speak OData, or your internal tools use a compact DSL — FlexQuery handles all of them through a unified parser pipeline.

### What It Is

The `QuerySyntax` system is a pluggable parser architecture that accepts raw query input in any supported format and converts it into a canonical `QueryOptions` abstract syntax tree (AST). All downstream processing (validation, expression building, SQL generation) operates on the same `QueryOptions` regardless of the input format.

### Why It Exists

Real-world APIs serve diverse clients. A React dashboard might send JSON filter objects, a legacy .NET client might use OData-style `$filter` strings, and a mobile app might use a compact query language. Without syntax abstraction, you would need separate endpoints or custom parsing logic for each client type.

### When to Use It

- You have clients that use different query formats
- You are migrating from OData or another query framework and need backwards compatibility
- You want to explicitly control which parser handles a request
- You are building a custom parser for a proprietary format

### When NOT to Use It

- If all your clients use the same format, you can rely on `AutoDetect` (the default) and never think about it
- If you are only passing pre-built `QueryOptions` objects programmatically (no string parsing needed)

## Architecture

```
HTTP Request
     │
     ▼
FlexQueryParameters        ← Raw string properties (Filter, Sort, Select, etc.)
     │
     ▼
QueryOptionsParser.Parse() ← Entry point
     │
     ├── QuerySyntax specified? ──Yes──► Use that parser directly
     │
     └── AutoDetect ──► Walk registered parsers
              │
              ├── Has indexed keys (filter[0].field)? ──► Generic/Indexed parser
              ├── parser.CanParse() returns true?     ──► Use that parser
              └── No match?                           ──► Fall back to last parser
              │
              ▼
         QueryOptions         ← Canonical AST used by all downstream processing
```

### Parser Registration

Parsers are registered in a prioritized list. New parsers registered via `RegisterParser()` are inserted at the **front** of the list, giving them first-match priority:

```csharp
// Built-in parsers (registered by default):
// 1. JqlQueryParser
// 2. JsonQueryParser
// 3. DslQueryParser

// MiniODataQueryParser is auto-registered if the assembly is loaded
// Custom parsers can be registered manually:
QueryOptionsParser.RegisterParser(new MyCustomParser());
```

## Supported Syntax Types

### NativeDsl — Colon-Separated Syntax

The most compact format. Fields, operators, and values are separated by colons:

```
filter=name:eq:john
filter=age:gt:18
filter=status:in:active,inactive
```

**Best for:** Internal tools, compact query strings, URL-friendly APIs.

### Json — Structured JSON Payloads

Sends complex, nested filter trees as JSON objects:

```json
{
  "logic": "and",
  "filters": [
    { "field": "Age", "operator": "gt", "value": 18 },
    { "field": "Status", "operator": "eq", "value": "Active" }
  ]
}
```

**Best for:** SPAs, complex multi-condition filters, programmatic query building.

### Jql — JQL-like Expression Strings

Human-readable expressions using standard operators:

```
filter=Age > 18 AND Status = 'Active'
filter=Name CONTAINS 'john' OR Email ENDSWITH '@acme.com'
```

**Best for:** Developer tools, debugging, admin panels, human-written queries.

### MiniOData — OData Compatibility

Standard OData `$filter` and `$orderby` syntax (requires `FlexQuery.NET.Parsers.MiniOData` package):

```
$filter=Age gt 18 and Status eq 'Active'
$orderby=Name desc
$select=Id,Name,Email
$expand=Orders
```

**Best for:** Migrating from OData, enterprise clients that already speak OData.

### Generic — Indexed Query Strings

Array-style query parameters common in form-based UIs:

```
filter[0].field=name&filter[0].operator=eq&filter[0].value=john
sort[0].field=age&sort[0].dir=desc
```

**Best for:** HTML forms, auto-generated query strings, framework integrations.

## Syntax Comparison Table

| Feature | NativeDsl | Json | Jql | MiniOData | Generic |
|---------|-----------|------|-----|-----------|---------|
| **Format** | `field:op:value` | JSON object | `Field OP Value` | `$filter=Field op Value` | `filter[0].field=X` |
| **Nested logic** | Limited | ✅ Full | ✅ Full | ✅ Full | ❌ Flat only |
| **URL-friendly** | ✅ Very | ❌ Needs body | ⚠️ Needs encoding | ✅ Good | ✅ Very |
| **Human-readable** | ⚠️ Medium | ❌ Low | ✅ High | ✅ High | ❌ Low |
| **Auto-detected by** | Colon separator | `{` prefix | Operator keywords | `$` key prefix | `[0]` index keys |
| **Package required** | Core | Core | Core | MiniOData | Core |

## Parser Selection Flow

The `QueryOptionsParser` selects a parser in this order:

1. **Explicit syntax** — If `QuerySyntax` is set to anything other than `AutoDetect`, that parser is used directly
2. **Indexed detection** — If the raw parameters contain indexed keys like `filter[0].field`, the Generic parser handles it
3. **CanParse probe** — Each registered parser's `CanParse()` method is called. The first one to return `true` wins
4. **Fallback** — If no parser claims the input, the last registered parser handles it

### Auto-Detection Rules

| Parser | Detects When |
|--------|-------------|
| **MiniOData** | Any raw parameter key starts with `$`, or filter contains OData operators (`eq`, `ne`, `contains(`) |
| **Json** | Filter string starts with `{` or `[` |
| **Jql** | Filter contains comparison operators (`=`, `>`, `<`, `!=`, `CONTAINS`, `AND`, `OR`) |
| **NativeDsl** | Filter contains colon-separated segments |
| **Generic** | Raw parameters have indexed keys like `filter[0]` |

## Real-World Example: Multi-Client API

A single endpoint that accepts requests from different client types:

```csharp
[HttpGet("products")]
public async Task<IActionResult> GetProducts(
    [FromQuery] FlexQueryParameters parameters)
{
    // No need to know which syntax the client used!
    // AutoDetect handles JSON, JQL, DSL, and OData transparently.
    var result = await _context.Products.FlexQueryAsync(parameters, opts =>
    {
        opts.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Category" };
        opts.MaxPageSize = 100;
    });

    return Ok(result);
}
```

This single endpoint handles all of these requests:
```
# JQL
GET /products?filter=Price > 50 AND Category = 'Electronics'

# NativeDsl
GET /products?filter=Price:gt:50

# MiniOData
GET /products?$filter=Price gt 50 and Category eq 'Electronics'

# JSON (via POST or encoded)
POST /products/query
{ "logic": "and", "filters": [{ "field": "Price", "operator": "gt", "value": 50 }] }
```

## Forcing a Specific Syntax

If you need to bypass auto-detection:

```csharp
var options = QueryOptionsParser.Parse(parameters, QuerySyntax.Json);
// Forces JSON parsing regardless of input format
```

## Building a Custom Parser

Implement `IQueryParser` to add support for a proprietary format:

```csharp
public class GraphQLFilterParser : IQueryParser
{
    public QuerySyntax Syntax => QuerySyntax.AutoDetect; // or a custom enum value

    public bool CanParse(FlexQueryParameters parameters)
    {
        return parameters.Filter?.Contains("{ query") == true;
    }

    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        // Parse your custom format into QueryOptions
        return new QueryOptions { /* ... */ };
    }
}

// Register at startup
QueryOptionsParser.RegisterParser(new GraphQLFilterParser());
```

## Best Practices

1. **Use AutoDetect** unless you have a specific reason to force a syntax — it handles mixed-client scenarios gracefully
2. **Validate the output, not the syntax** — Apply `AllowedFields`, `MaxFieldDepth`, and `StrictFieldValidation` on the parsed `QueryOptions`, not on the raw input
3. **Standardize internally** — If all your clients are under your control, pick one syntax (typically JSON or JQL) and document it
4. **Use MiniOData for migration only** — It exists for backwards compatibility. For new APIs, prefer JSON or JQL
5. **Cache is syntax-aware** — The parser cache uses the syntax type as part of the cache key, so the same filter string parsed as JQL vs DSL produces different cache entries

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Sending JSON in a query string without encoding | URL-encode the JSON, or use a POST body |
| Mixing OData and FlexQuery parameters in the same request | The parser detects `$` prefix and routes to MiniOData. Don't mix `$filter` with `filter` |
| Assuming case sensitivity | All operators are case-insensitive. `EQ`, `eq`, and `Eq` are all valid |
| Registering a custom parser but it never gets selected | Check your `CanParse()` logic. Custom parsers are inserted first but must return `true` for their inputs |

## Related Features

- [Filtering](/guide/filtering) — How parsed filters become LINQ expressions
- [MiniOData Adapter](/adapters/miniodata) — OData compatibility details
- [AG Grid Adapter](/adapters/ag-grid) — Parsing AG Grid JSON payloads
