# Field Mapping

## Overview

Field mapping is FlexQuery's mechanism for decoupling your public API contract from your internal database schema. It allows you to expose friendly, stable field names to API consumers while maintaining full control over how those fields resolve to database expressions internally.

### What It Is

`MapField<TEntity, TProperty>()` is a method on `BaseQueryOptions` that registers a mapping from an external alias string to an internal LINQ expression. When a client uses that alias in a filter, sort, or projection request, FlexQuery substitutes the registered expression transparently.

### Why It Exists

In production systems, the shape of your API rarely matches the shape of your database. You might need to:

- **Expose computed values** — a `FullName` field that concatenates `FirstName` and `LastName`
- **Hide legacy schemas** — a column named `cust_nm` exposed as `CustomerName`
- **Support API versioning** — maintaining `v1` field names after a database migration
- **Enforce security boundaries** — never exposing raw database column names to external clients

Without field mapping, any of these scenarios would require custom middleware, manual filter rewriting, or separate DTO layers with manual translation.

### When to Use It

- You have DTO field names that differ from entity property names
- You want to expose computed or derived fields for filtering and sorting
- You need to support legacy database schemas behind a clean API
- You need per-endpoint field aliasing without modifying your entity model

### When NOT to Use It

- Your DTO and entity have identical property names — the engine resolves them automatically
- You only need simple field renaming — consider `FieldMappings` (a `Dictionary<string, string>`) instead of expression-based mapping
- Internal-only queries where no external API contract exists

## Architecture

Field mapping operates at the expression tree level. When `MapField()` is called, FlexQuery stores the LINQ expression in the `ExpressionMappings` dictionary on `BaseQueryOptions`. During query execution, when the expression builder encounters a field name that exists in this dictionary, it substitutes the registered `LambdaExpression` instead of performing a standard property access.

```
Client Request: ?filter=FullName eq 'John Doe'
                            │
                            ▼
                   ExpressionMappings lookup
                   "FullName" → c => c.FirstName + " " + c.LastName
                            │
                            ▼
               Expression tree built with concatenation
                            │
                            ▼
            EF Core / Dapper translates to SQL:
            WHERE [FirstName] + ' ' + [LastName] = @p0
```

This happens at the LINQ expression level, so it is fully translatable by EF Core and the Dapper `SqlTranslator`.

## The MapField API

```csharp
public void MapField<TEntity, TProperty>(
    string alias,
    Expression<Func<TEntity, TProperty>> expression)
```

| Parameter | Description |
|-----------|-------------|
| `alias` | The external field name clients will use in filter, sort, and select parameters |
| `expression` | A LINQ expression defining how to resolve the field against the entity |

The alias is matched **case-insensitively** by default.

## Basic Example

Map a single property to a different name:

```csharp
var execOptions = new QueryExecutionOptions();

// Expose "CustomerName" even though the entity property is "Name"
execOptions.MapField<Customer, string>("CustomerName", c => c.Name);
```

Clients can now query with:
```
GET /api/customers?filter=CustomerName eq 'Acme Corp'&sort=CustomerName:asc
```

## Advanced Example: Computed Fields

Map to a computed expression that doesn't exist as a single column:

```csharp
execOptions.MapField<Customer, string>(
    "FullName",
    c => c.FirstName + " " + c.LastName);

execOptions.MapField<Order, decimal>(
    "LineTotal",
    o => o.Quantity * o.UnitPrice);

execOptions.MapField<Product, bool>(
    "IsExpensive",
    p => p.Price > 1000m);
```

These computed fields can be filtered, sorted, and projected just like real columns:

```
GET /api/orders?filter=LineTotal gt 500&sort=LineTotal:desc
```

## Real-World Example: Legacy Database Schema

Consider a legacy database where column names are abbreviated or poorly named:

```csharp
// Entity matches the legacy database schema
public class LegacyCustomer
{
    public int CustId { get; set; }
    public string CustNm { get; set; }
    public string CustEmail { get; set; }
    public DateTime CrtDt { get; set; }
}

// Clean API exposure
execOptions.MapField<LegacyCustomer, int>("Id", c => c.CustId);
execOptions.MapField<LegacyCustomer, string>("Name", c => c.CustNm);
execOptions.MapField<LegacyCustomer, string>("Email", c => c.CustEmail);
execOptions.MapField<LegacyCustomer, DateTime>("CreatedAt", c => c.CrtDt);
```

The API now exposes clean field names while the database schema remains untouched:

```
GET /api/customers?select=Id,Name,Email&filter=CreatedAt gt '2024-01-01'
```

## Nested Property Mapping

Map external aliases to navigation property paths:

```csharp
execOptions.MapField<Order, string>(
    "CompanyName",
    o => o.Customer.Company.Name);

execOptions.MapField<Order, string>(
    "ShipCity",
    o => o.ShippingAddress.City);
```

This generates proper JOIN SQL when used with Dapper, or nested property access with EF Core.

## Validation Behavior

Field mappings are validated alongside all other field access rules:

- If `AllowedFields` is set, mapped aliases must be included in the allowed set
- If `StrictFieldValidation` is `true`, using an unmapped and unrecognized alias throws a validation error
- If `StrictFieldValidation` is `false`, unrecognized aliases are silently removed

```csharp
execOptions.AllowedFields = new HashSet<string> { "Id", "FullName", "Email" };
execOptions.MapField<Customer, string>("FullName", c => c.FirstName + " " + c.LastName);

// ✅ Works: "FullName" is in AllowedFields and has a mapping
// ❌ Blocked: "InternalScore" is not in AllowedFields
```

## Filtering, Sorting, and Projection

Mapped fields work seamlessly across all query operations:

```csharp
execOptions.MapField<Product, decimal>("DiscountedPrice", p => p.Price * (1 - p.DiscountRate));
```

| Operation | Query | Behavior |
|-----------|-------|----------|
| **Filter** | `?filter=DiscountedPrice lt 50` | Translates to `WHERE [Price] * (1 - [DiscountRate]) < @p0` |
| **Sort** | `?sort=DiscountedPrice:desc` | Translates to `ORDER BY [Price] * (1 - [DiscountRate]) DESC` |
| **Projection** | `?select=Name,DiscountedPrice` | Includes the computed expression in the SELECT clause |

## Security Benefits

Field mapping is a security-hardening tool:

1. **Schema hiding** — Clients never learn actual database column names
2. **Attack surface reduction** — Only explicitly mapped fields are accessible
3. **Input validation** — Unmapped fields are rejected (in strict mode) or silently removed
4. **Principle of least privilege** — Combine with `AllowedFields` to expose only what each endpoint needs

## Simple Alias Mapping vs Expression Mapping

For simple renames (no computed expressions), you can use the lighter-weight `FieldMappings` dictionary:

```csharp
// Simple string-to-string aliasing (no LINQ expressions)
execOptions.FieldMappings = new Dictionary<string, string>
{
    ["CustomerName"] = "Name",
    ["CustomerEmail"] = "Email"
};

// Expression-based mapping (for computed or nested fields)
execOptions.MapField<Customer, string>("FullName", c => c.FirstName + " " + c.LastName);
```

Use `FieldMappings` when the relationship is a simple rename. Use `MapField()` when you need computed logic.

## Performance Considerations

- Expression mappings are resolved during expression tree construction, **not at runtime per-row**
- The mapped expressions are compiled into the same LINQ tree as standard filters, so there is no additional runtime overhead
- Expression caching applies equally to queries that use mapped fields
- Complex computed expressions (e.g., multiple string concatenations) may produce less efficient SQL depending on your database engine — profile in production

## Best Practices

1. **Register mappings centrally** — Define all field mappings in a shared configuration method or DI registration, not scattered across controllers
2. **Use AllowedFields alongside MapField** — Mapping a field does not automatically allow it; explicitly whitelist mapped aliases
3. **Keep expressions database-translatable** — Avoid calling C# methods that cannot be translated to SQL (e.g., custom extension methods without an EF Core translation)
4. **Document your mappings** — Since mapped field names differ from entity property names, document the available query fields in your API specification or Swagger metadata
5. **Test with real providers** — Expression translatability can vary between EF Core providers (SQL Server vs PostgreSQL) and Dapper dialects

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Mapping a field but forgetting to add it to `AllowedFields` | The field will be blocked by validation. Always add mapped aliases to the allowed set. |
| Using C#-only methods in the expression (e.g., `Regex.IsMatch`) | These cannot be translated to SQL. Use database-compatible operations only. |
| Conflicting aliases with real property names | If both a real property `Name` and a mapping for `Name` exist, the mapping takes precedence. Be intentional about overlaps. |
| Registering mappings per-request instead of once | This works but wastes resources. Register in DI or a static initializer. |

## Related Features

- [Validation & Field Access](/guide/validation) — How `AllowedFields` and `StrictFieldValidation` interact with mapped fields
- [Security & Governance](/guide/security) — Broader security model including `BlockedFields`, `RoleAllowedFields`, and `FieldAccessResolver`
- [Filtering](/guide/filtering) — How filter expressions consume field mappings
- [Projection](/guide/projection) — How SELECT uses mapped fields
