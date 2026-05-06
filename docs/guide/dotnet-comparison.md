# Comparison: FlexQuery.NET vs .NET Query Libraries

This page compares FlexQuery.NET with other popular .NET dynamic query libraries: **Gridify**, **Sieve**, and **System.Linq.Dynamic.Core**.

---

## Overview

| | FlexQuery.NET | Gridify | Sieve | System.Linq.Dynamic.Core |
| :--- | :--- | :--- | :--- | :--- |
| **Primary purpose** | Full query engine | Filter + sort + page | Filter + sort + page | Dynamic LINQ |
| **Input format** | DSL, JQL, JSON, Indexed | Custom DSL string | Custom DSL string | LINQ expression strings |
| **Validation** | ✅ Built-in pipeline | ❌ None | ⚠️ Attribute-based | ❌ None |
| **Projection** | ✅ Dynamic field select | ❌ None | ❌ None | ✅ Dynamic select |
| **Aggregates** | ✅ sum, count, avg | ❌ None | ❌ None | ✅ via dynamic LINQ |
| **Filtered includes** | ✅ Independent pipeline | ❌ None | ❌ None | ❌ None |
| **Grouping** | ✅ GROUP BY + HAVING | ❌ None | ❌ None | ✅ via dynamic LINQ |
| **Field security** | ✅ Allow/Block/Depth | ❌ None | ✅ Attribute-based | ❌ None |
| **EF Core support** | ✅ Full async pipeline | ✅ IQueryable | ✅ IQueryable | ✅ IQueryable |
| **SQL injection safety** | ✅ Expression trees | ✅ Expression trees | ✅ Expression trees | ⚠️ String-based — risk |
| **Multiple formats** | ✅ 4 formats | ❌ One format | ❌ One format | ❌ String only |
| **OpenAPI DTO** | ✅ FlexQueryParameters | ❌ Raw string | ❌ Model per class | ❌ Raw string |

---

## The Same Query — Four Libraries

**Goal:** Get users where status is "active" and age >= 18, sorted by name ascending, page 2, 10 per page.

### FlexQuery.NET

```
GET /api/users?filter=status:eq:active&age:gte:18&sort=name:asc&page=2&pageSize=10
```

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "name", "status", "age" };
    });
    return Ok(result);
}
```

---

### Gridify

```
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

**Limitations:**
- No field validation — any field can be filtered.
- No projection — always returns full entity.
- No aggregates, no filtered includes.

---

### Sieve

```
GET /api/users?filters=Status==active,Age>=18&sorts=Name&page=2&pageSize=10
```

```csharp
public class UserSieveProcessor : SieveProcessor
{
    public UserSieveProcessor(IOptions<SieveOptions> options) : base(options) { }
}

// User entity must be annotated
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

**Limitations:**
- Security declared via `[Sieve]` attribute on entity — must modify entity class.
- No projection, no aggregates, no filtered includes.
- Limited to one format.

---

### System.Linq.Dynamic.Core

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers(
    string? filter, string? sort, int page = 1, int pageSize = 10)
{
    var query = _context.Users.AsQueryable();

    if (!string.IsNullOrEmpty(filter))
        query = query.Where(filter); // ⚠️ string-based — SQL injection risk

    if (!string.IsNullOrEmpty(sort))
        query = query.OrderBy(sort);

    var total = await query.CountAsync();
    var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    return Ok(new { data, total, page, pageSize });
}
```

**Limitations:**
- String-based LINQ expressions — **SQL injection risk if user input is not sanitized**.
- No validation, no field security, no projection, no aggregates.
- Requires you to build pagination, counting, and response envelope yourself.

---

## Feature Matrix

| Feature | FlexQuery.NET | Gridify | Sieve | Dynamic.Core |
| :--- | :---: | :---: | :---: | :---: |
| Filter | ✅ | ✅ | ✅ | ✅ |
| Sort | ✅ | ✅ | ✅ | ✅ |
| Page | ✅ | ✅ | ✅ | ❌ (manual) |
| Projection (select) | ✅ | ❌ | ❌ | ✅ |
| Filtered includes | ✅ | ❌ | ❌ | ❌ |
| Aggregates | ✅ | ❌ | ❌ | ✅ |
| GROUP BY | ✅ | ❌ | ❌ | ✅ |
| Field allow-list | ✅ | ❌ | ✅ (attribute) | ❌ |
| Field block-list | ✅ | ❌ | ❌ | ❌ |
| Depth limit | ✅ | ❌ | ❌ | ❌ |
| Role-based fields | ✅ | ❌ | ❌ | ❌ |
| Validation pipeline | ✅ | ❌ | ❌ | ❌ |
| Multiple input formats | ✅ (4) | ❌ | ❌ | ❌ |
| OpenAPI-compatible DTO | ✅ | ✅ | ✅ | ❌ |
| Async EF Core support | ✅ | ✅ | ✅ | ❌ (manual) |
| Expression tree safety | ✅ | ✅ | ✅ | ⚠️ |
| QueryResult envelope | ✅ | ✅ | ❌ | ❌ |
| Cache key generation | ✅ | ❌ | ❌ | ❌ |

---

## Tradeoffs

### FlexQuery.NET
**Strengths:** Full query engine, field security, multiple formats, projection, aggregates, validation.
**Tradeoffs:** More concepts to learn upfront. Heavier than Gridify for trivial filter-only use cases.

### Gridify
**Strengths:** Extremely simple to set up. Zero configuration. Good for CRUD-only APIs.
**Tradeoffs:** No security, no projection, no aggregates, no validation. Not suitable for public APIs.

### Sieve
**Strengths:** Attribute-based security is clean. Familiar for teams coming from ASP.NET conventions.
**Tradeoffs:** Requires annotating entity classes. No projection, no aggregates, limited operators.

### System.Linq.Dynamic.Core
**Strengths:** Extremely flexible — any LINQ expression can be dynamic.
**Tradeoffs:** String-based expressions carry SQL injection risk. No built-in security, validation, or pagination. Requires significant boilerplate.

---

## When to Choose

| Scenario | Best Choice |
| :--- | :--- |
| Simple internal CRUD API, no security needed | Gridify |
| Attribute-annotated entities, basic filtering | Sieve |
| Advanced dynamic LINQ with full flexibility | System.Linq.Dynamic.Core |
| Public API with security, projection, validation, aggregates | **FlexQuery.NET** |
| Multi-tenant SaaS with per-role field access | **FlexQuery.NET** |
| Dashboard / reporting endpoint with grouping | **FlexQuery.NET** |
