> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Extension Methods

FlexQuery.NET is built entirely around `IQueryable&lt;T&gt;` extension methods. This ensures it plays nicely with Entity Framework Core (or any other LINQ provider) without forcing you to inherit from base controllers or rewrite your data access layer.

This page documents the core extension methods and when to use each one.

## 1. `ApplyValidatedQueryOptions` (Recommended)

This is the all-in-one method. It runs the 8-step security validation pipeline, applies Filtering, Sorting, Includes, Pagination, *and* Projection in a single call.

Because it handles Projection (`select`), it returns an `IQueryable<dynamic>`.

**Example:**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    var options = QueryOptionsParser.Parse(request);
    
    // Secure it
    options.BlockedFields = new HashSet<string> { "Password" };

    // Execute everything
    var data = await _context.Users
        .ApplyValidatedQueryOptions(options)
        .ToListAsync();

    return Ok(data);
}
```

## 2. `ToProjectedQueryResultAsync` (EF Core package)

If you have installed the `FlexQuery.NET.EFCore` package, this is the most powerful method available. It automatically executes a `CountAsync()` for your total records, followed by a `ToListAsync()` for the paginated window, and returns a structured Paged Result DTO.

**Example:**
```csharp
var pagedResult = await _context.Products.ToProjectedQueryResultAsync(options);

/* Returns:
{
    "TotalCount": 1450,
    "Page": 1,
    "PageSize": 10,
    "TotalPages": 145,
    "Items": [ ... dynamic projected items ... ]
}
*/
return Ok(pagedResult);
```

## 3. `ApplyQueryOptions` (No Projection)

If you *never* want clients to shape the payload (i.e., you don't want to support `select`), or if you need to map the result to a strongly-typed AutoMapper DTO *after* filtering, use `ApplyQueryOptions`.

Unlike the methods above, this returns your original `IQueryable&lt;T&gt;`, meaning strongly-typed execution.

**Example:**
```csharp
// Returns IQueryable<User> (NO PROJECTION)
var query = _context.Users.AsQueryable();
var query = query.ApplyQueryOptions(options);

var dtos = await query
    .Select(u => new UserDto { Id = u.Id, Name = u.Name }) // Safe manual projection
    .ToListAsync();
```

## 4. `ApplySelect` (Manual Projection)

If you used `ApplyQueryOptions` to handle filtering and sorting, but you *do* want to allow client-side projection later in the pipeline, you can chain `ApplySelect`.

**Example:**
```csharp
var query = _context.Orders.AsQueryable();
var query = query.ApplyQueryOptions(options);

// ... do some custom business logic or manual Includes ...
query = query.Include(o => o.Customer);

// Apply dynamic projection at the end
var projected = await query.ApplySelect(options).ToListAsync();
```

## 5. Security Validation Methods

If you are not using `ApplyValidatedQueryOptions` (which does this automatically), you should manually validate the options before executing them against your database.

### `.Validate()`
Returns a Validation Result object. Non-throwing.
```csharp
var validation = options.Validate(typeof(Customer));
if (!validation.IsValid) 
{
    return BadRequest(validation.Errors);
}
```

### `.EnsureValid()`
Throws a `QueryValidationException` if the request violates your security constraints.
```csharp
try 
{
    options.EnsureValid(typeof(Customer));
    var data = await _context.Customers.ApplyQueryOptions(options).ToListAsync();
}
catch (QueryValidationException ex)
{
    return BadRequest(ex.Result.Errors);
}
```

