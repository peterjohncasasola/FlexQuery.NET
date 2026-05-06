
# Comparison: FlexQuery.NET vs .NET Query Libraries

This page compares FlexQuery.NET with several popular .NET query libraries including:

- Gridify
- Sieve
- System.Linq.Dynamic.Core

The goal is not to declare a “winner”, but to clarify the different design philosophies, strengths, and tradeoffs of each approach.

Different libraries solve different problems.

---

# Overview

| | FlexQuery.NET | Gridify | Sieve | System.Linq.Dynamic.Core |
| :--- | :--- | :--- | :--- | :--- |
| Primary focus | Unified query pipeline | Lightweight filtering | Attribute-based filtering | Dynamic LINQ expressions |
| Input style | DSL, JQL, JSON, Indexed | Custom DSL | Query model | LINQ expression strings |
| Projection (`select`) | ✅ | ❌ | ❌ | ✅ |
| Grouping / Aggregates | ✅ | ❌ | ❌ | ✅ |
| Filtered includes | ✅ | ❌ | ❌ | ❌ |
| Validation pipeline | ✅ Built-in | ❌ External | ⚠️ Attribute-based | ❌ External |
| Field-level restrictions | ✅ | ❌ | ⚠️ Attribute-based | ❌ |
| Multiple query formats | ✅ | ❌ | ❌ | ❌ |
| Async EF Core pipeline | ✅ | ✅ | ✅ | ⚠️ Manual composition |
| OpenAPI-friendly DTO | ✅ | ✅ | ✅ | ❌ |

---

# Different Philosophies

## FlexQuery.NET

FlexQuery.NET is designed as a higher-level query framework focused on:

- API-driven querying
- validation
- projection
- grouping
- aggregates
- field-level access control
- reusable query pipelines

It is intended for scenarios where query safety, flexibility, and composability are important.

Typical use cases:
- public APIs
- multi-tenant systems
- reporting endpoints
- admin dashboards
- advanced search systems

---

## Gridify

Gridify focuses on simplicity and minimal setup.

It provides lightweight filtering, sorting, and paging with a small API surface and quick onboarding experience.

Typical use cases:
- internal CRUD APIs
- admin tools
- lightweight filtering scenarios
- rapid prototyping

Applications requiring projection, aggregates, or field-level validation may require additional infrastructure.

---

## Sieve

Sieve uses attribute-based configuration to enable filtering and sorting.

It integrates naturally with ASP.NET-style conventions and works well for teams preferring declarative entity configuration.

Typical use cases:
- attribute-driven APIs
- simple filtering/sorting requirements
- convention-based applications

---

## System.Linq.Dynamic.Core

System.Linq.Dynamic.Core provides highly flexible runtime LINQ expression execution using string-based expressions.

It is extremely powerful for advanced dynamic query generation scenarios.

Typical use cases:
- runtime-generated LINQ
- advanced admin tooling
- dynamic report builders
- expression-driven systems

Because expressions are string-based, applications typically need their own validation and restriction layers for public-facing APIs.

---

# The Same Query Across Libraries

Goal:

- status == "active"
- age >= 18
- sort by name ascending
- page 2
- page size 10

---

## FlexQuery.NET

```http
GET /api/users?filter=status:eq:active&age:gte:18&sort=name:asc&page=2&pageSize=10
```

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, exec =>
    {
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

## Gridify

```http
GET /api/users?filter=status=active,age>=18&orderBy=name&page=2&pageSize=10
```

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] GridifyQuery query)
{
    var result = await _context.Users.GridifyAsync(query);
    return Ok(result);
}
```

Gridify intentionally keeps configuration lightweight and focused on filtering/sorting/paging.

---

## Sieve

```http
GET /api/users?filters=Status==active,Age>=18&sorts=Name&page=2&pageSize=10
```

```csharp
public class UserSieveProcessor : SieveProcessor
{
    public UserSieveProcessor(IOptions<SieveOptions> options)
        : base(options)
    {
    }
}

public class User
{
    [Sieve(CanFilter = true, CanSort = true)]
    public string Name { get; set; }

    [Sieve(CanFilter = true)]
    public string Status { get; set; }

    [Sieve(CanFilter = true)]
    public int Age { get; set; }
}

[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] SieveModel model)
{
    var query = _sieveProcessor.Apply(model, _context.Users);
    return Ok(await query.ToListAsync());
}
```

Sieve emphasizes declarative configuration through attributes.

---

## System.Linq.Dynamic.Core

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers(
    string? filter,
    string? sort,
    int page = 1,
    int pageSize = 10)
{
    var query = _context.Users.AsQueryable();

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

Dynamic.Core provides maximum flexibility, but applications typically implement their own validation, paging, and restriction layers.

---

# Feature Matrix

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
| Strongly-typed query model | ✅ | ❌ | ❌ | ❌ |

---

# Tradeoffs

## FlexQuery.NET

### Strengths
- Unified query pipeline
- Projection support
- Aggregates and grouping
- Validation and field-level restrictions
- Multiple query formats
- Strongly-typed query model

### Tradeoffs
- More concepts to learn initially
- Heavier than lightweight filtering libraries
- More configuration surface area

---

## Gridify

### Strengths
- Extremely simple setup
- Minimal configuration
- Lightweight API surface
- Fast onboarding experience

### Tradeoffs
- Focused primarily on filtering/sorting/paging
- Advanced query scenarios may require additional infrastructure

---

## Sieve

### Strengths
- Declarative attribute-based configuration
- Familiar ASP.NET-style conventions
- Clean integration with entity models

### Tradeoffs
- Requires entity annotations
- Limited advanced query features
- Primarily focused on filtering/sorting

---

## System.Linq.Dynamic.Core

### Strengths
- Extremely flexible
- Full runtime LINQ expression support
- Powerful dynamic query generation

### Tradeoffs
- String-based expressions can become difficult to validate
- Public APIs often require additional restriction layers
- Paging/projection pipelines are typically composed manually

---

# Choosing the Right Tool

| Scenario | Recommended Approach |
| :--- | :--- |
| Simple internal CRUD filtering | Gridify |
| Attribute-driven filtering | Sieve |
| Runtime-generated LINQ expressions | Dynamic.Core |
| Public APIs with validation/projection | FlexQuery.NET |
| Multi-tenant field-restricted APIs | FlexQuery.NET |
| Reporting endpoints with grouping/aggregates | FlexQuery.NET |

---

# Final Thoughts

Each library optimizes for different priorities:

| Library | Primary Priority |
| :--- | :--- |
| Gridify | Simplicity |
| Sieve | Declarative conventions |
| Dynamic.Core | Flexibility |
| FlexQuery.NET | Unified query pipeline |

FlexQuery.NET is designed for applications requiring more than simple filtering — particularly scenarios involving:

- projection
- grouping
- aggregates
- validation
- field-level access control
- reusable query pipelines

For lightweight CRUD filtering, smaller libraries may provide a simpler experience.

For advanced API querying scenarios, FlexQuery.NET aims to provide a more complete query abstraction layer.
