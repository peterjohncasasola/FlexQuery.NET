# Query Composition

FlexQuery.NET is designed from the ground up to support two complementary query models: **string-based queries** for external API input and **strongly-typed programmatic composition** for internal server-side logic.

Unlike many query libraries that treat their internal object models as implementation details, FlexQuery.NET intentionally exposes its structured query model as a first-class advanced API. This enables sophisticated runtime query generation, reusable query templates, and hybrid filtering scenarios that are difficult to achieve with raw string manipulation.

---

## Query API Levels

FlexQuery.NET provides three distinct levels of interaction, allowing you to choose the right balance of abstraction and control for your specific scenario.

| API Level | Recommended For | Entry Point |
| :--- | :--- | :--- |
| **High-Level API** | Controllers, APIs, frontend-driven filtering | `FlexQueryAsync` / `FlexQueryParameters` |
| **Mid-Level API** | Composable pipelines, query augmentation | `Apply()` extension |
| **Advanced API** | Dynamic runtime query composition | `QueryOptions` / `FilterGroup` / `SortNode` |

---

## Why Query Composition Matters

Real-world applications often require more than just passing a user's filter to the database. Query composition is essential for:

- **Multi-Tenant Restrictions**: Automatically injecting a `TenantId` filter into every query to ensure data isolation.
- **Permission Injection**: Restricting result sets based on the current user's roles or visibility level.
- **Soft-Delete Enforcement**: Ensuring that "deleted" records are excluded unless explicitly requested.
- **Business Rule Enforcement**: Forcing specific sorting orders or pagination limits for compliance.
- **Dynamic Report Builders**: Constructing complex, nested queries based on a UI-driven query builder.

---

## String-Based Querying

String-based queries are the standard for external API interaction. They are lightweight, URL-friendly, and easy to consume from frontend frameworks.

```http
GET /api/customers?filter=status:eq:active&sort=createdDate:desc&page=1&pageSize=20
```

FlexQuery.NET parses this into a structured `QueryOptions` object, which is then validated against your server-side security policies before execution.

---

## Strongly-Typed Query Composition

For internal logic, you can construct a `QueryOptions` object manually. This provides full type safety, refactor-safety, and access to complex logical structures.

```csharp
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Builders;

var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Logic = LogicOperator.And,
        Filters = new List<FilterCondition>
        {
            new FilterCondition
            {
                Field = "Status",
                Operator = FilterOperators.Equal,
                Value = "Active"
            },
            new FilterCondition
            {
                Field = "Salary",
                Operator = FilterOperators.GreaterThan,
                Value = "50000"
            }
        }
    },
    Sort = new List<SortNode>
    {
        new SortNode
        {
            Field = "CreatedAt",
            Descending = true
        },
        new SortNode
        {
            Field = "Name",
            Descending = false
        }
    },
    Paging = new PagingOptions
    {
        Page = 1,
        PageSize = 20
    }
};

// Apply directly to any IQueryable
var results = await QueryBuilder
    .Apply(query, options)
    .ToListAsync();
```

---

## Nested Query Groups

Programmatic composition makes it easy to build deeply nested hierarchical logic that would be cumbersome as a query string.

```csharp
var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Logic = LogicOperator.And,
        Groups = new List<FilterGroup>
        {
            // Group 1: (City == 'New York' AND Status == 'Active')
            new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "City", Operator = "eq", Value = "New York" },
                    new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                }
            },
            // Group 2: (Salary > 50000)
            new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "Salary", Operator = "gt", Value = "50000" }
                }
            }
        }
    }
};
```

---

## Hybrid Query Composition

One of the most powerful features of FlexQuery.NET is the ability to **augment** a user-provided query with server-side business rules. This is far safer than string concatenation and ensures your security rules are always applied.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    // 1. Parse the user's query from the URL
    var options = parameters.ToQueryOptions();

    // 2. Inject a mandatory tenant restriction
    options.Filter ??= new FilterGroup { Logic = LogicOperator.And };
    options.Filter.Groups.Add(new FilterGroup
    {
        Logic = LogicOperator.And,
        Filters = new List<FilterCondition>
        {
            new FilterCondition
            {
                Field = "TenantId",
                Operator = FilterOperators.Equal,
                Value = _currentUser.TenantId
            }
        }
    });

    // 3. Enforce a deterministic default sort order
    options.Sort.Add(new SortNode
    {
        Field = "CreatedAt",
        Descending = true
    });

    // 4. Execute the resulting hybrid query
    var result = await _context.Orders.FlexQueryAsync(options);
    return Ok(result);
}
```

---

## QueryOptions as an AST

`QueryOptions` acts as a structured **Abstract Syntax Tree (AST)** for your query. Because it is a standard C# object model, your application can:

- **Inspect**: Analyze what fields a user is trying to filter or sort by.
- **Modify**: Rewrite field names, redirect paths, or inject conditions.
- **Validate**: Apply custom cross-field validation rules that the standard validator might not cover.
- **Serialize**: Store complex queries in a database or cache for later reuse.

This structured approach is inherently safer than raw string manipulation, as it prevents injection attacks and ensures the query remains syntactically valid regardless of how many modifications you apply.

---

## Sorting & Paging Composition

Direct composition of sorting and paging is useful for runtime augmentation but should be used judiciously.

### Sorting
```csharp
var options = new QueryOptions
{
    Sort = new List<SortNode>
    {
        new SortNode { Field = "CreatedAt", Descending = true }
    }
};
```

### Paging
```csharp
var options = new QueryOptions
{
    Paging = new PagingOptions { Page = 1, PageSize = 20 }
};
```

> [!TIP]
> If your query behavior is fixed and known at compile time, standard LINQ methods (`.OrderBy()`, `.Skip()`, `.Take()`) are usually simpler. Use `QueryOptions` when the query structure must be determined at runtime.

---

## Fluent Builder API

FlexQuery.NET includes a strongly-typed fluent builder for developers who prefer a more discoverable, IntelliSense-driven experience.

```csharp
using FlexQuery.NET;

var results = await _context.Customers
    .Filter<User>(f => f
        .And(x => x.IsActive).Eq(true)
        .AndGroup(g => g
            .Field(x => x.Role).Eq("Admin")
            .Or(x => x.Permission).Contains("Write")
        )
    )
    .ToListAsync();
```

---

## Comparison Table

| Feature | String Queries | QueryOptions / Builders |
| :--- | :--- | :--- |
| **API Input** | Primary Use Case | Not Recommended |
| **Type Safety** | Low (Parsed at runtime) | High (Compile-time checked) |
| **Runtime Composition** | Medium (String building) | Excellent (Object manipulation) |
| **Refactor Safety** | Low | High |
| **Nested Logic** | Harder to read/write | Natural and hierarchical |

---

## Recommendations

### Use String Queries for:
- External public APIs and REST endpoints.
- Frontend data grids and search bars.
- Scenarios where users need to share filtered views via URL.

### Use QueryOptions and Composition for:
- Internal service-to-service communication.
- Enforcing global business rules (Tenancy, Soft-Delete).
- Building complex, dynamic search interfaces with runtime logic.

---

## Final Notes

Most applications should start with the high-level `FlexQueryAsync` entry point:

```csharp
var result = await query.FlexQueryAsync(parameters);
```

However, as your application grows in complexity, leveraging `QueryOptions` directly allows you to build a truly dynamic and secure data access layer.
