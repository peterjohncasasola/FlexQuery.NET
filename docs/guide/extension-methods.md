# Extension Methods

FlexQuery.NET is built entirely around `IQueryable<T>` extension methods. This ensures it plays nicely with Entity Framework Core (or any other LINQ provider) without forcing you to inherit from base controllers or rewrite your data access layer.

This page documents the core extension methods and when to use each one.

## 1. `FlexQueryAsync` (Recommended)

This is the all-in-one unified pipeline method. It handles parsing, validation, filtering, sorting, paging, includes, and projection in a single secure call.

**Example:**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    // Unified pipeline execution
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
    });

    return Ok(result);
}
```

## 2. `Apply` (Low-Level All-in-One)

Applies Filter, Sort, and Paging in sequence. It returns the modified `IQueryable<T>`. It does **not** apply projection or validation.

**Example:**
```csharp
var options = QueryOptionsParser.Parse(parameters);

// Returns IQueryable<User> (Filtered, Sorted, and Paged)
var query = _context.Users.AsQueryable();
var query = query.Apply(options);

var data = await query.ToListAsync();
```

## 3. Atomic Pipeline Methods

Use these when you need to apply only specific parts of the FlexQuery logic.

### `.ApplyFilter(options)`
Applies the `WHERE` clause.
```csharp
query = query.ApplyFilter(options);
```

### `.ApplySort(options)`
Applies the `ORDER BY` clause.
```csharp
query = query.ApplySort(options);
```

### `.ApplyPaging(options)`
Applies `SKIP` and `TAKE`.
```csharp
query = query.ApplyPaging(options);
```

### `.ApplySelect(options)`
Applies the dynamic projection (`select`). Returns `IQueryable<object>`.
```csharp
var projected = query.ApplySelect(options);
```

### `.ApplyFilteredIncludes(options)`
Applies EF Core filtered includes (e.g., `Include(x => x.Orders.Where(...))`).
```csharp
query = query.ApplyFilteredIncludes(options);
```

---

## 4. Security & Validation

If you are not using `FlexQueryAsync`, you should manually validate the options before executing them.

### `.ValidateOrThrow<T>(execOptions)`
Throws a `QueryValidationException` if validation fails.
```csharp
options.ValidateOrThrow<User>(execOptions);
```

### `.ValidateSafe<T>(execOptions)`
Returns a `ValidationResult`. Non-throwing.
```csharp
var result = options.ValidateSafe<User>(execOptions);
if (!result.IsValid)
{
    return BadRequest(result.Errors);
}
```
