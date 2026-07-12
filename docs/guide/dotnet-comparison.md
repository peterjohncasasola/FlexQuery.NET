# Comparison: FlexQuery.NET vs .NET Query Libraries

## Overview

This page compares FlexQuery.NET with several popular .NET query libraries including:
- Gridify
- Sieve
- System.Linq.Dynamic.Core

The goal is not to declare a universal "winner", but to clarify the different design philosophies, strengths, and tradeoffs of each approach. Different libraries solve different problems.

## Why this comparison exists

When building dynamic APIs in .NET, teams often evaluate a spectrum of tools ranging from simple string-to-LINQ mappers to full execution engines. This guide exists to help architects understand where FlexQuery.NET sits on that spectrum, particularly compared to heavily utilized libraries like `System.Linq.Dynamic.Core` and `Gridify`. 

## When to choose which

- **Simple internal CRUD filtering**: Gridify
- **Attribute-driven filtering**: Sieve
- **Runtime-generated LINQ expressions**: Dynamic.Core
- **Public APIs with validation/projection**: FlexQuery.NET
- **Reporting endpoints with grouping/aggregates**: FlexQuery.NET

---

## High-Level Matrix

| | FlexQuery.NET | Gridify | Sieve | System.Linq.Dynamic.Core |
| :--- | :--- | :--- | :--- | :--- |
| **Primary focus** | Unified query pipeline | Lightweight filtering | Attribute-based filtering | Dynamic LINQ expressions |
| **Input style** | DSL, FQL | Custom DSL | Query model | LINQ expression strings |
| **Projection (`select`)** | ✅ | ❌ | ❌ | ✅ |
| **Grouping / Aggregates**| ✅ | ❌ | ❌ | ✅ |
| **Filtered includes** | ✅ | ❌ | ❌ | ❌ |
| **Validation pipeline** | ✅ Built-in | ❌ External | ⚠️ Attribute-based | ❌ External |
| **Field restrictions** | ✅ | ❌ | ⚠️ Attribute-based | ❌ |
| **Multiple formats** | ✅ | ❌ | ❌ | ❌ |
| **Async EF Core pipeline**| ✅ | ✅ | ✅ | ⚠️ Manual composition |
| **OpenAPI-friendly DTO** | ✅ | ✅ | ✅ | ❌ |

---

## Different Philosophies

### FlexQuery.NET

FlexQuery.NET is designed as a higher-level query framework focused on:
- API-driven querying
- validation
- projection (SELECT)
- grouping and aggregates
- field-level access control
- reusable query pipelines

It is intended for scenarios where query safety, flexibility, and composability are paramount, such as public APIs, multi-tenant systems, and reporting endpoints.

---

### Gridify

Gridify focuses on simplicity and minimal setup. It provides lightweight filtering, sorting, and paging with a small API surface and quick onboarding experience. 

It is ideal for internal CRUD APIs and rapid prototyping. Applications requiring projection, aggregates, or field-level validation may require additional custom infrastructure on top of Gridify.

---

### Sieve

Sieve uses attribute-based configuration to enable filtering and sorting. It integrates naturally with ASP.NET-style conventions and works well for teams preferring declarative entity configuration.

It is best suited for attribute-driven APIs and simple filtering/sorting requirements.

---

### System.Linq.Dynamic.Core

System.Linq.Dynamic.Core provides highly flexible runtime LINQ expression execution using string-based expressions. It is extremely powerful for advanced dynamic query generation scenarios (like runtime report builders).

Because expressions are string-based and parsed blindly, applications typically need to build their own extensive validation and restriction layers to expose it safely to public-facing APIs.

---

## The Same Query Across Libraries

**Goal:**
- `status == "active"`
- `age >= 18`
- Sort by `name` ascending
- Page 2, Page size 10

---

### FlexQuery.NET

```http
GET /api/customers?filter=status:eq:active%26salary:gte:50000&sort=name:asc&page=2&pageSize=10
```

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        // Enforce server policy
        exec.AllowedFields = new HashSet<string>
        {
            "name",
            "status",
            "age"
        };
    });

    return Ok(result);
}
```

---

### Gridify

```http
GET /api/customers?filter=status=active,salary>=50000&orderBy=name&page=2&pageSize=10
```

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] GridifyQuery query)
{
    var result = await _context.Customers.GridifyAsync(query);
    return Ok(result);
}
```

Gridify intentionally keeps configuration lightweight and focused purely on filtering/sorting/paging.

---

### Sieve

```http
GET /api/customers?filters=Status==active,Salary>=50000&sorts=Name&page=2&pageSize=10
```

```csharp
public class CustomerSieveProcessor : SieveProcessor
{
    public CustomerSieveProcessor(IOptions<SieveOptions> options) : base(options) { }
}

public class Customer
{
    [Sieve(CanFilter = true, CanSort = true)]
    public string Name { get; set; }

    [Sieve(CanFilter = true)]
    public string Status { get; set; }

    [Sieve(CanFilter = true)]
    public decimal Salary { get; set; }
}

[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] SieveModel model)
{
    var query = _sieveProcessor.Apply(model, _context.Customers);
    return Ok(await query.ToListAsync());
}
```

Sieve emphasizes declarative configuration through attributes.

---

### System.Linq.Dynamic.Core

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers(
    string? filter,
    string? sort,
    int page = 1,
    int pageSize = 10)
{
    var query = _context.Customers.AsQueryable();

    if (!string.IsNullOrEmpty(filter))
        query = query.Where(filter);

    if (!string.IsNullOrEmpty(sort))
        query = query.OrderBy(sort);

    var total = await query.CountAsync();

    var data = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(new
    {
        data,
        total,
        page,
        pageSize
    });
}
```

Dynamic.Core provides maximum flexibility, but applications typically implement their own validation, paging, and projection layers.

---

## Feature Matrix

| Feature | FlexQuery.NET | Gridify | Sieve | Dynamic.Core |
| :--- | :---: | :---: | :---: | :---: |
| Filtering | ✅ | ✅ | ✅ | ✅ |
| Sorting | ✅ | ✅ | ✅ | ✅ |
| Paging | ✅ | ✅ | ✅ | ⚠️ Manual |
| Projection (`select`) | ✅ | ❌ | ❌ | ✅ |
| Grouping | ✅ | ❌ | ❌ | ✅ |
| Aggregates | ✅ | ❌ | ❌ | ✅ |
| Filtered includes | ✅ | ❌ | ❌ | ❌ |
| Field allow/block lists | ✅ | ❌ | ⚠️ Attribute-based | ❌ |
| Validation pipeline | ✅ | ❌ | ⚠️ Partial | ❌ |
| Multiple query formats | ✅ | ❌ | ❌ | ❌ |
| OpenAPI DTO | ✅ | ✅ | ✅ | ❌ |
| Query result envelope | ✅ | ⚠️ Partial | ❌ | ❌ |
| Async EF Core support | ✅ | ✅ | ✅ | ⚠️ Manual |
| Strongly-typed AST | ✅ | ❌ | ❌ | ❌ |

---

## Tradeoffs

### FlexQuery.NET

**Strengths**
- Unified query pipeline that handles everything from parsing to SQL execution.
- Projection support (dynamic SELECT) minimizes database I/O.
- Built-in validation and field-level restrictions.
- Standardized REST envelope (`QueryResult<T>`).

**Tradeoffs**
- More concepts to learn initially compared to minimal libraries.

### Gridify

**Strengths**
- Extremely simple setup with minimal configuration.
- Lightweight API surface and fast onboarding experience.

**Tradeoffs**
- Advanced query scenarios (Projection, Aggregates, Included filtering) require additional infrastructure.

### Sieve

**Strengths**
- Declarative attribute-based configuration fits nicely with Entity configuration.
- Clean integration with ASP.NET conventions.

**Tradeoffs**
- Requires heavy entity annotations.
- Limited advanced query features.

### System.Linq.Dynamic.Core

**Strengths**
- Full runtime LINQ expression support makes it exceptionally powerful.

**Tradeoffs**
- Public APIs often require additional restriction layers to prevent malicious strings.
- Paging/projection pipelines are typically composed manually.
