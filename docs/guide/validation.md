# Validation

## Overview

FlexQuery.NET validates every query before execution. The validation pipeline checks field paths, operators, access rules, and depth — and returns structured errors.

## Why this feature exists

Client-provided query strings are inherently untrusted input. The validation layer ensures that no unauthorized field names, operators, or navigation path depths can reach the LINQ expression builder or the database. This is the security boundary between the public API and your database schema.

---

## What the Validation Pipeline Checks

1. **Field existence** — Does the field exist on the entity?
2. **Operator validity** — Is the operator compatible with the field's data type?
3. **Field access** — Is the field in `AllowedFields`, `FilterableFields`, etc.?
4. **Depth** — Does the path exceed `MaxFieldDepth`?
5. **Blocked fields** — Is the field in `BlockedFields`?
6. **Role-based access** — Does the current role allow this field?

---

## Validation Methods

### ValidateOrThrow (Throw on Failure)

```csharp
options.ValidateOrThrow<Customer>(execOptions);
// Throws QueryValidationException if invalid
```

### ValidateSafe (Return Result)

```csharp
var result = options.ValidateSafe<Customer>(execOptions);

if (!result.IsValid)
{
    return BadRequest(new { errors = result.Errors });
}
```

### With Global Configuration

Global defaults are configured once at startup via `FlexQueryCore.Configure`. The validator automatically merges global defaults with per-request overrides:

```csharp
// In Program.cs - global defaults
FlexQueryCore.Configure(options =>
{
    options.MaxPageSize = 1000;
    options.MaxFieldDepth = 5;
});

// In controller - per-request override only
var execOptions = new QueryExecutionOptions
{
    MaxFieldDepth = 2,  // Override global (5)
    AllowedFields = new HashSet<string> { "id", "name", "email" }
};
```

---

## ValidationResult

```csharp
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = new();
}

public class ValidationError
{
    public string Message { get; }
    public string Code    { get; }
    public string Field   { get; }
}
```

---

## Sample Error Outputs

### Invalid Field (Access Denied)

**Request:**
```
GET /api/customers?filter=email:isnotnull
```

**Response (400):**
```json
{
  "errors": [
    {
      "message": "Field 'passwordHash' is explicitly blocked.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "passwordHash"
    }
  ]
}
```

### Field Not in AllowedFields

**Request:**
```
GET /api/customers?filter=salary:gt:50000
```

**Response (400):**
```json
{
  "errors": [
    {
      "message": "Field 'salary' is not in the global allowed list.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "salary"
    }
  ]
}
```

### Depth Exceeded

**Request:**
```
GET /api/customers?filter=address.city:eq:New York
```

**Response (400):**
```json
{
  "errors": [
    {
      "message": "Field path 'profile.address.city.postcode' exceeds maximum allowed depth of 2.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "profile.address.city.postcode"
    }
  ]
}
```

### Field Not Filterable

**Request:**
```
GET /api/customers?filter=createdDate:gt:2024-01-01
```

With `exec.FilterableFields = { "name", "status" }` (createdAt not included):

**Response (400):**
```json
{
  "errors": [
    {
      "message": "Field 'createdAt' is not allowed for Filter operation.",
      "code": "FIELD_ACCESS_DENIED",
      "field": "createdAt"
    }
  ]
}
```

---

## Per-Field Operator Governance

You can restrict which operators are allowed for specific fields. This is useful for preventing expensive scans on certain columns (e.g., blocking `Contains` on a large text field).

```csharp
var execOptions = new QueryExecutionOptions();

// Only allow Equal and StartsWith on Email
execOptions.AllowOperators("Email", FilterOperators.Equal, FilterOperators.StartsWith);

// Allow standard comparisons on Age
execOptions.AllowOperators("Age", FilterOperators.Equal, FilterOperators.GreaterThan, FilterOperators.LessThan);
```

If a client attempts to use a restricted operator:

**Request:**
```
GET /api/customers?filter=email:contains:gmail.com
```

**Response (400):**
```json
{
  "errors": [
    {
      "message": "Operator 'contains' is not allowed for field 'Email'.",
      "code": "OPERATOR_NOT_ALLOWED",
      "field": "Email"
    }
  ]
}
```

By default, if a field is not configured in `AllowedOperators`, all supported operators remain available.

---

## Strict vs. Lenient Validation

### StrictFieldValidation = true (default recommended)

Throws on the **first** violation. Faster, fail-fast behavior.

```csharp
exec.StrictFieldValidation = true;
```

### StrictFieldValidation = false

Collects **all** violations and returns them together.

```csharp
exec.StrictFieldValidation = false;
var result = options.ValidateSafe<User>(exec);
// result.Errors may contain multiple entries
```

---

## Controller Integration Pattern

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "id", "name", "email", "status" },
        BlockedFields = new HashSet<string> { "passwordHash" },
        MaxFieldDepth = 2
    };

    var result = await _context.Customers.FlexQueryAsync(parameters, execOptions);
    return Ok(result);
}
```

Or with inline configuration:

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email", "status", "city" };
        exec.BlockedFields = new HashSet<string> { "email" };
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

---

## Nested Filter Validation

The validation pipeline traverses the full filter AST, including nested AND/OR groups and scoped collection filters.

```
filter = (orders:any:status:eq:blockedField)
```

This validates `blockedField` within the scoped `orders` collection context.

---

## Best Practices

- Always call `ValidateOrThrow<T>` or `ValidateSafe<T>` **before** any database call.
- Use `ValidateSafe<T>` to return client-friendly error details.
- Set `StrictFieldValidation = true` in production.
- Always configure `AllowedFields` or `BlockedFields` — do not rely on entity reflection alone.
- Return `400 Bad Request` for validation failures, not `500`.