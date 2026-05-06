> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Query Validation

FlexQuery.NET executes an 8-step pre-execution validation pipeline. It validates your query *before* attempting to hit the database to prevent catastrophic SQL exceptions or data leaks.

## Fail-Fast Validation

If a query violates a security rule (like accessing a blocked field) or is syntactically invalid, FlexQuery.NET throws a `QueryValidationException`. 

### Catching and Returning Validation Errors

You should catch this exception and return a structured `400 Bad Request` to the client.

**Backend (C#):**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    try 
    {
        var options = QueryOptionsParser.Parse(request);
        options.BlockedFields = new HashSet<string> { "PasswordHash" };

        var users = await _context.Users
            .ApplyValidatedQueryOptions(options)
            .ToListAsync();

        return Ok(users);
    }
    catch (QueryValidationException ex)
    {
        // Return structured errors
        return BadRequest(new {
            isValid = false,
            errors = ex.Result.Errors.Select(e => new {
                code = e.Code,
                field = e.Field,
                message = e.Message
            })
        });
    }
}
```

## Example Validation Errors

### 1. Requesting a Blocked Field

**Request:**
```http
GET /api/users?select=Id,PasswordHash
```

**Response (400 Bad Request):**
```json
{
  "isValid": false,
  "errors": [
    {
      "code": "FIELD_ACCESS_DENIED",
      "field": "PasswordHash",
      "message": "Field 'PasswordHash' is explicitly blocked."
    }
  ]
}
```

### 2. Requesting a Non-Existent Field

**Request:**
```http
GET /api/users?filter=Age:gt:20&sort=FakeField:asc
```

**Response (400 Bad Request):**
```json
{
  "isValid": false,
  "errors": [
    {
      "code": "FIELD_NOT_FOUND",
      "field": "FakeField",
      "message": "Field 'FakeField' does not exist on type 'User'."
    }
  ]
}
```

### 3. Invalid Type Comparison

**Request:**
Trying to compare a boolean field against a string array.
```http
GET /api/users?filter=IsActive:in:Yes,No
```

**Response (400 Bad Request):**
```json
{
  "isValid": false,
  "errors": [
    {
      "code": "TYPE_MISMATCH",
      "field": "IsActive",
      "message": "Cannot apply 'in' operator to boolean field."
    }
  ]
}
```

