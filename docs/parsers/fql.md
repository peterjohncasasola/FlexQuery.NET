# FQL Parser

## Overview

`FlexQuery.NET.Parsers.Fql` is an optional parser package that enables FlexQuery to understand and parse SQL-like query expressions. FQL (FlexQuery Query Language) provides an expressive, familiar syntax for filtering, sorting, grouping, aggregation, and projection while integrating seamlessly with the FlexQuery.NET query pipeline.

### What It Is

FQL is a SQL-inspired query language parser package. It translates human-readable query expressions into FlexQuery's canonical `QueryOptions` AST. It does **not** execute SQL directly — it generates the same `QueryOptions` that all FlexQuery parsers produce, which are then executed by your chosen provider (EF Core or Dapper).

### Why It Exists

Many developers prefer SQL-like syntax for ad-hoc queries, admin tools, and developer-facing APIs. FQL lets you accept natural language expressions (`Status = 'Active' AND Salary >= 50000`) instead of colon-separated DSL (`status:eq:active AND salary:gte:50000`), making APIs more accessible to users familiar with SQL.

### When to Use It

- You want SQL-like query syntax for your API consumers
- You are building developer tools, admin panels, or internal APIs
- Your users are more comfortable with SQL expressions than DSL syntax
- You want readable query strings in logs and debugging tools

### When NOT to Use It

- You are building a public API where URL compactness is critical — use native DSL instead
- All your clients are under your control and can use the native DSL format
- You need OData compatibility — use MiniOData instead

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.Fql
```

## Registration

```csharp
using FlexQuery.NET.Parsers.Fql;

Fql.Register();
```

This registers the `FqlQueryParser` with the `QueryOptionsParser` and enables the FQL syntax. Must be called **once** at startup.

**Important:** In v4, parser packages are framework-agnostic and require explicit static registration. The old `AddFqlParser()` DI extension was removed.

## Supported Operators

### Comparison Operators

| FQL Operator | Meaning | Example |
|:---|:---|:---|
| `=` | Equal | `Status = 'Active'` |
| `!=` | Not equal | `Status != 'Inactive'` |
| `>` | Greater than | `Salary > 50000` |
| `>=` | Greater than or equal | `Salary >= 50000` |
| `<` | Less than | `Salary < 100000` |
| `<=` | Less than or equal | `Salary <= 100000` |

### Logical Operators

| FQL Operator | Meaning | Example |
|:---|:---|:---|
| `AND` | Logical AND | `Status = 'Active' AND Salary > 50000` |
| `OR` | Logical OR | `City = 'New York' OR City = 'London'` |

### String Operators

| FQL Operator | Meaning | Example |
|:---|:---|:---|
| `CONTAINS` | String contains | `Name CONTAINS 'john'` |
| `STARTSWITH` | String starts with | `Email STARTSWITH 'admin'` |
| `ENDSWITH` | String ends with | `Email ENDSWITH '.com'` |
| `LIKE` | SQL LIKE pattern | `Name LIKE '%john%'` |

### Null Operators

| FQL Operator | Meaning | Example |
|:---|:---|:---|
| `IS NULL` | Is null | `Address IS NULL` |
| `IS NOT NULL` | Is not null | `Address IS NOT NULL` |

### Collection Operators

| FQL Operator | Meaning | Example |
|:---|:---|:---|
| `IN` | Value in set | `Status IN ('Active', 'Pending', 'Review')` |
| `NOT IN` | Value not in set | `Status NOT IN ('Deleted', 'Banned')` |
| `BETWEEN` | Value in range | `Salary BETWEEN 50000 AND 100000` |
| `ANY` | Collection contains matching element | `Orders.Any(Status = 'Shipped')` |
| `ALL` | All collection elements match | `Orders.All(Status = 'Shipped')` |
| `COUNT` | Collection count comparison | `Orders.Count > 5` |

## Supported Query Parameters

| Parameter | Description | Example |
|:---|:---|:---|
| `filter` | Filter records using FQL syntax | `filter=Status = 'Active' AND Salary > 50000` |
| `select` | Select specific fields | `select=Id,Name,Email` |
| `sort` | Sort results | `sort=Name asc,CreatedDate desc` |
| `include` | Include related entities | `include=Orders` |
| `groupBy` | Group results | `groupBy=Status` |
| `aggregates` | Aggregate functions | `aggregates=COUNT(*),SUM(Salary)` |
| `having` | HAVING condition | `having=SUM(Salary) > 10000` |
| `page` | Page number | `page=1` |
| `pageSize` | Items per page | `pageSize=20` |
| `distinct` | Return distinct records | `distinct=true` |
| `includeCount` | Include total count | `includeCount=true` |

## Basic Example

```csharp
using FlexQuery.NET.Parsers.Fql;

// Register at startup
Fql.Register();

FlexQueryCore.Configure(options =>
{
    options.QuerySyntax = QuerySyntax.Fql;
});
```

```http
GET /api/customers?filter=Status = 'Active' AND Salary >= 50000&sort=Name asc&page=1&pageSize=20
```

```csharp
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET;

[HttpGet("customers")]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "Id", "Name", "Email", "Status", "City", "Salary", "CreatedDate"
        };
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

## Nested Logic

FQL supports deeply nested AND/OR groups:

```
GET /api/customers?filter=(City = 'New York' OR City = 'London') AND Status = 'Active' AND Salary > 50000
```

Equivalent to:

```sql
WHERE (City = 'New York' OR City = 'London')
  AND Status = 'Active'
  AND Salary > 50000
```

## Collection Navigation

### ANY

Filter customers who have at least one shipped order:

```
GET /api/customers?filter=Orders.Any(Status = 'Shipped')
```

With additional conditions:

```
GET /api/customers?filter=Orders.Any(Status = 'Shipped' AND Total > 100)
```

### Nested Collections

Filter customers with shipped orders containing expensive items:

```
GET /api/customers?filter=Orders.Any(
    Status = 'Shipped'
    AND OrderItems.Any(Total > 100)
)
```

### COUNT

Filter customers with more than 5 orders:

```
GET /api/customers?filter=Orders.Count > 5
```

### ALL

Filter customers where all orders are shipped:

```
GET /api/customers?filter=Orders.All(Status = 'Shipped')
```

## String Operations

### CONTAINS

```
GET /api/customers?filter=Name CONTAINS 'alice'
```

### STARTSWITH / ENDSWITH

```
GET /api/customers?filter=Email STARTSWITH 'admin'
GET /api/customers?filter=Email ENDSWITH '.com'
```

### LIKE

```
GET /api/customers?filter=Name LIKE '%john%'
```

## Real-World Example: Complex Query

Retrieve active customers from New York or London with salary above 50000, including their recent shipped orders:

```
GET /api/customers?
  filter=(City = 'New York' OR City = 'London')
    AND Status = 'Active'
    AND Salary > 50000
  &include=Orders(Status = 'Shipped')
  &select=Id,Name,Email,City,Salary,Orders.OrderNumber,Orders.Total
  &sort=Salary desc,Name asc
  &page=1&pageSize=20
  &includeCount=true
```

## Comparison with Other Formats

| Feature | FQL | NativeDsl | MiniOData |
|:---|:---|:---|:---|
| **Syntax style** | SQL-like | Colon-separated | OData `$` parameters |
| **Nested logic** | ✅ Full AND/OR | ✅ Full AND/OR | ✅ Full AND/OR |
| **Collection operators** | ✅ Any, All, Count | ✅ any, all, count | ❌ Not supported |
| **URL-friendly** | ⚠️ Needs encoding | ✅ Very | ✅ Good |
| **Human-readable** | ✅ High | ⚠️ Medium | ✅ High |
| **Package required** | `FlexQuery.NET.Parsers.Fql` | Core (default) | `FlexQuery.NET.Parsers.MiniOData` |
| **Registration** | `Fql.Register()` | None | `MiniOData.Register()` |
| **Best for** | Developer tools, admin panels | Internal tools, compact URLs | OData migration |


To explicitly use FQL, either:
- Configure globally: `FlexQueryCore.Configure(options => options.QuerySyntax = QuerySyntax.Fql)`
- Or send FQL-style expressions with the `query` parameter instead of `filter`

## Performance Considerations

- FQL parsing is ~1.8× slower than DSL due to more complex tokenization (handling quotes, whitespace, operator precedence)
- Still sub-microsecond per request — negligible overhead for most applications
- Parser results are cached by `QueryOptionsParser` using a composite cache key that includes the syntax type

## Security Considerations

- FQL expressions can reference any field name — **always** configure `AllowedFields` and `StrictFieldValidation` to prevent field enumeration
- FQL supports nested property access (`Customer.Address.City`) — use `MaxFieldDepth` to limit traversal depth
- Collection operators (`ANY`, `ALL`) can generate expensive queries — validate and test with realistic data volumes

## Best Practices

1. **Pick one syntax per API** — If all clients are under your control, standardize on DSL or FQL
2. **Use FQL for developer-facing tools** — The SQL-like syntax is more accessible for ad-hoc queries
3. **Validate aggressively** — FQL's expressiveness makes it important to enforce `AllowedFields` and `MaxFieldDepth`
4. **Document supported operators** — Not all FQL operators may be enabled in your security policy
5. **Consider URL encoding** — FQL expressions with spaces and quotes need proper URL encoding in query strings

## Common Pitfalls

| Pitfall | Solution |
|:---|:---|
| Forgetting to call `Fql.Register()` | FQL queries throw `ParserNotRegisteredException` at runtime |
| Mixing DSL and FQL syntax | DSL uses colons (`field:op:value`), FQL uses spaces and operators (`field OP value`) |
| Not URL-encoding FQL expressions | Spaces, quotes, and parentheses must be URL-encoded in query strings |
| Assuming all operators are enabled | `AllowedOperators` restricts which FQL operators are accepted |
| Nested property depth exceeded | `MaxFieldDepth` limits how deep FQL can traverse object graphs |

## Related Features

- [Query Syntax](/guide/query-syntax) — How to configure and register parsers
- [Query Formats](/guide/query-formats) — Comparing DSL, FQL, JSON, and MiniOData
- [Filtering](/guide/filtering) — FlexQuery's full filter operator reference
- [Operators Reference](/shared/operators) — Complete operator documentation
- [MiniOData Parser](/parsers/miniodata) — OData compatibility alternative