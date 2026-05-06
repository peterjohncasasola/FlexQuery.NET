# Validation

FlexQuery.NET validates every query before execution. The validation pipeline checks field paths, operators, access rules, and depth — and returns structured errors.

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
options.ValidateOrThrow<User>(execOptions);
// Throws QueryValidationException if invalid
```

### ValidateSafe (Return Result)

```csharp
var result = options.ValidateSafe<User>(execOptions);

if (!result.IsValid)
{
    return BadRequest(new { errors = result.Errors });
}
```

### On IQueryable

```csharp
var result = _context.Users.Validate(options, execOptions);
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
GET /api/users?filter=passwordHash:isnotnull
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
GET /api/users?filter=salary:gt:50000
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
GET /api/users?filter=profile.address.city.postcode:eq:12345
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
GET /api/users?filter=createdAt:gt:2024-01-01
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
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var options = QueryOptionsParser.Parse(parameters);

    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "id", "name", "email", "status" },
        BlockedFields = new HashSet<string> { "passwordHash" },
        MaxFieldDepth = 2
    };

    var validation = options.ValidateSafe<User>(execOptions);
    if (!validation.IsValid)
    {
        return BadRequest(new
        {
            title = "Query validation failed",
            errors = validation.Errors.Select(e => new
            {
                field   = e.Field,
                code    = e.Code,
                message = e.Message
            })
        });
    }

    var query = _context.Users.AsQueryable();
    // ... rest of pipeline
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
