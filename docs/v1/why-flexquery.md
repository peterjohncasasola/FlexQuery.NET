> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Why FlexQuery.NET?

> FlexQuery.NET turns your API into a secure, composable query engine — not just a filtering helper.

Building flexible and scalable APIs often requires a robust way to handle dynamic user input for filtering, sorting, and projecting data. FlexQuery.NET was created to solve the common challenges developers face when building these systems.

## The Problem

In a typical .NET API, handling multiple query parameters often leads to repetitive and brittle code. You might find yourself writing logic like this for every endpoint:

```csharp
public async Task<IActionResult> Get(string? name, string? status, int? minAge)
{
    var query = _context.Users.AsQueryable();

    if (!string.IsNullOrEmpty(name))
        query = query.Where(x => x.Name.Contains(name));

    if (!string.IsNullOrEmpty(status))
        query = query.Where(x => x.Status == status);

    if (minAge.HasValue)
        query = query.Where(x => x.Age >= minAge.Value);

    return Ok(await query.ToListAsync());
}
```

### Why this doesn't scale:
- **Repetitive**: You have to manually add `if` blocks for every new filter field.
- **Hardcoded**: Adding a new filter requires a backend code change and deployment.
- **Inconsistent**: Different developers might implement filters differently across endpoints.
- **Complexity**: Supporting nested logic (AND/OR) or collection filtering (`any`/`all`) manually is extremely difficult and error-prone.

## The Solution

FlexQuery.NET shifts the responsibility of defining the query from the backend developer to the API client, while giving the server full control over security and performance.

Instead of dozens of specific parameters, your API provides a single, unified query interface:

```http
GET /api/customers?query=name contains "John" AND status = "Active" AND age >= 18
```

The library parses this string into an optimized LINQ Expression Tree that is executed directly by your database provider (like EF Core).

## ⚙️ Execution Model (What Makes It Different)

Unlike simple "string-to-linq" helpers, FlexQuery.NET is built as a layered query engine, providing multiple levels of abstraction to suit your needs:

### 1. Low-level pipeline
Methods like `ApplyFilter`, `ApplySort`, `ApplyPaging`, and `ApplySelect` give you atomic control over every step of the transformation.

### 2. Mid-level pipeline
Methods like `ApplyQueryOptions` and `ApplyValidatedQueryOptions` combine these steps into a single call, adding security and validation boundaries.

### 3. High-level execution
Result wrappers like `ToProjectedQueryResult` and `ToProjectedQueryResultAsync` handle the entire lifecycle—from parsing and validation to projection and final database execution—in a single optimized trip.

This layered design allows you to choose the perfect balance between developer convenience and enterprise-level control.

## 🧠 Choosing the Right Approach

| Scenario | Recommended Method |
| :--- | :--- |
| **Basic filtering API** | `ApplyValidatedQueryOptions` |
| **API with projection (select/include)** | `ToProjectedQueryResultAsync` |
| **Full control / custom pipeline** | Low-level atomic methods |
| **Public API** | Always use **Validation** |

## ⚠️ Validation vs. Execution

Understanding the distinction between these two stages is critical for both security and performance:

- **ApplyValidatedQueryOptions**: Focuses on **validation + filtering**. It returns an `IQueryable&lt;T&gt;` for further processing.
- **ToProjectedQueryResultAsync**: Focuses on **execution + projection**. It performs a single-trip SQL query but does **NOT** run validation automatically.

> [!CAUTION]
> **Important**: These should **NOT** be combined with the same options object (e.g., calling `ApplyValidatedQueryOptions` and then passing that query to `ToProjectedQueryResultAsync`). Doing so causes "Double Filtering," where the same logic is applied twice to the SQL query.

## Key Benefits

- **Dynamic Filtering**: Support any combination of filters (nested AND/OR, collection predicates) without changing a single line of C# code.
- **Validation Engine**: Built-in type safety and field-existence checks before the query ever hits the database.
- **Field-Level Security**: Enterprise-grade whitelisting (`AllowedFields`) and blacklisting (`BlockedFields`) to protect sensitive data.
- **Dual Pipeline Architecture**: Decouples root filtering (WHERE) from data shaping (Filtered Includes) to solve the "over-filtering" problem.
- **Grouping & Aggregates**: Powerful server-side analytical queries (`group`, `sum`, `count`, `avg`) via simple query parameters.
- **Data Shaping (Projection)**: Allow clients to request only specific fields or flattened result sets, reducing database and network overhead.

## Before vs After

### Before
Manual LINQ construction with many condition checks and specific DTOs for every query permutation.

### After
A single, clean pipeline:
```csharp
var options = QueryOptionsParser.Parse(Request.Query);
var result = await _context.Users.ToProjectedQueryResultAsync(options);
```

## 💡 Why This Design?

FlexQuery.NET intentionally separates **validation**, **filtering**, **projection**, and **execution**. This modularity is a core differentiator, providing:

- **Flexibility**: Compose custom pipelines that fit your specific business logic.
- **Composability**: Combine dynamic query logic with standard EF Core LINQ.
- **Enterprise-Level Control**: Fine-grained security rules that respect your domain model and data access layers.

## Philosophy

FlexQuery.NET is more than a helper library—it is a **query abstraction layer for APIs**.

It is built on the philosophy that:
1.  **Clients should define the shape and scope of the data** they need to solve their specific UI requirements.
2.  **Servers should enforce the rules and boundaries** of those queries to ensure security, performance, and data integrity.

## When to Use FlexQuery.NET

- **Flexible APIs**: When building public or internal REST APIs that need to support varying user requirements.
- **Admin Dashboards**: Powering complex grids, tables, and search interfaces with multi-column filtering and sorting.
- **Reporting Systems**: Enabling users to build custom analytical views of data with aggregates and grouping.
- **Dynamic UI Filters**: When your frontend uses a visual query builder or dynamic filter sets that change frequently.

## When NOT to Use FlexQuery.NET

- **Simple CRUD**: For basic endpoints that always return the same fixed set of data.
- **Fixed Queries**: When the business logic requires a very specific, static query that will never change.
- **Minimal Logic Apps**: In small applications where a few simple `if` statements are easier to maintain.
- **Ultra Performance-Critical Endpoints**: In paths where every microsecond of parsing overhead is unacceptable and dynamic flexibility is not a requirement.

## Summary

FlexQuery.NET solves the "Dynamic Querying" problem by providing a secure, performant, and consistent way to translate client intent into database queries. It reduces boilerplate, improves developer productivity, and makes your APIs significantly more powerful.

