# DTO Field Mapping

## Overview

When exposing an API, it is an industry best practice to separate your external Data Transfer Objects (DTOs) from your internal database entities. `FlexQuery.NET` provides a seamless way to map public DTO fields to internal entity expressions using the `MapField` method on your execution options.

## Why this feature exists

If your API exposes a `CustomerDto`, but you execute `FlexQueryAsync` against a `Customer` database entity, clients might try to filter or sort by fields that exist on the DTO but *not* on the entity (like a computed property). Conversely, if you rename a database column from `strFullName` to `FirstName`, you don't want to break existing API clients that are still filtering on `?filter=fullName:eq:Alice`. 

By using `MapField`, you define exactly how an external field string should be translated into an internal `IQueryable` LINQ expression before it hits the database.

## When to use

- When your API contract differs from your database schema (e.g., camelCase vs PascalCase, or different property names entirely).
- When you want to expose a "computed" field to the client that can be filtered and sorted on the server.
- When you need to provide an alias for a deeply nested relationship to simplify client queries.

---

## Complete Runnable Example

You configure mappings directly inside the options lambda passed to `FlexQueryAsync`.

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, options =>
    {
        // 1. Basic mapping: The external field "name" maps to the internal "FullName"
        options.MapField<Customer, string>("name", c => c.FullName);

        // 2. Computed mapping: The external field "displayName" maps to a computed concatenation
        options.MapField<Customer, string>("displayName", c => c.FirstName + " " + c.LastName);

        // 3. Navigation mapping: The external field "company" maps to a nested property
        options.MapField<Customer, string>("company", c => c.Company.Name);

        // Security: Don't forget to explicitly allow the mapped field aliases!
        options.AllowedFields = ["name", "displayName", "company", "Id"];
    });

    return Ok(result);
}
```

### The Client Request

```http
GET /api/customers?filter=displayName:contains:'John' AND company:eq:AcmeCorp&sort=displayName:asc
```

### How it Works (Under the Hood)

When the query is parsed, FlexQuery checks the client's requested fields against your mapping dictionary. 

If a client requests filtering on `displayName`, the query engine intercepts `displayName` and substitutes the LINQ expression `c => c.FirstName + " " + c.LastName`. 

Entity Framework Core then translates that expression into the appropriate SQL (e.g. `WHERE [FirstName] + ' ' + [LastName] LIKE '%John%'`). This completely abstracts your database schema from the API contract, and works natively across both EF Core and the Dapper provider.

---

## Best Practices

- **Security First:** Mapped fields are subject to the same validation pipeline as normal fields. If you map `"displayName"`, you must still include `"displayName"` in your `AllowedFields` whitelist, or the request will be rejected.
- **Dapper Limitations:** While EF Core can translate complex inline C# expressions, the Dapper `SqlTranslator` can only translate standard SQL-compatible binary expressions (like string concatenation `+`, math operators, and coalescing). Avoid complex C# method calls (like `.ToString()`) inside a `MapField` expression if you are using the Dapper provider.
- **Alias Collisions:** Ensure your mapped aliases do not collide with actual physical properties on the entity, unless your explicit intent is to shadow and override the default behavior.
