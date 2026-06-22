# DTO Field Mapping

When exposing an API, it's best practice to separate your external Data Transfer Objects (DTOs) from your internal database entities. `FlexQuery.NET` provides a seamless way to map public DTO fields to internal entity expressions using `MapField`.

## Why Map Fields?

If your API exposes a `CustomerDto`, but queries the database against a `Customer` entity, clients might try to filter or sort by fields that exist on the DTO but not on the entity, or they might try to access sensitive database columns.

By using `MapField`, you define exactly how an external field string should be translated into an internal `IQueryable` LINQ expression.

## Example Usage

```csharp
var options = new BaseQueryOptions();

// Basic mapping: The external field "Name" maps to the internal "FullName"
options.MapField<Customer, string>("Name", c => c.FullName);

// Complex mapping: The external field "FullName" maps to a computed expression
options.MapField<Customer, string>("FullName", c => c.FirstName + " " + c.LastName);

// Navigation mapping: The external field "CompanyName" maps to a nested property
options.MapField<Customer, string>("CompanyName", c => c.Company.Name);
```

### How it Works

When a query is parsed, if a client requests sorting or filtering on `FullName` (e.g. `?$filter=FullName eq 'John Doe'`), the query engine will intercept `FullName` and substitute the LINQ expression `c => c.FirstName + " " + c.LastName`.

This completely abstracts your database schema from the API contract, and works natively across EF Core and Dapper.
