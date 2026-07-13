# Extension Methods

## Overview

FlexQuery.NET is built entirely around `IQueryable<T>` extension methods. This ensures it integrates seamlessly with Entity Framework Core (or any LINQ provider) without forcing you to inherit from base controllers or rewrite your data access layer.

## Why this feature exists

Extension methods preserve the idiomatic .NET style. Your controller logic stays clean and declarative, and the query composition remains fully compatible with any pre-existing `IQueryable` chain you have already constructed (e.g., `_context.Products.Where(p => p.TenantId == tenantId).FlexQueryAsync(...)`).

---

## Core Pipeline (`IQueryable<T>`)

### `FlexQueryAsync` ⭐ Recommended

The all-in-one unified pipeline method. Handles parsing, validation, filtering, sorting, paging, includes, and projection in a single secure call.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = ["Id", "Name", "Email"];
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

```

---

## Request Parsing

### `.ToQueryOptions()`
Converts an incoming `FlexQueryParameters`, `FlexQueryRequest`, or `MiniODataRequest` into the canonical `QueryOptions` AST. This is typically the first step if you are building a custom pipeline.

```csharp
var options = parameters.ToQueryOptions();
```

---

## Atomic Pipeline Methods

Use these when you need to apply only specific parts of the FlexQuery logic in a custom orchestration.

### `.ApplyFilter(options)`
Applies the `WHERE` clause from the parsed AST.
```csharp
query = query.ApplyFilter(options);
```

### `.ApplySort(options)`
Applies the `ORDER BY` clause.
```csharp
query = query.ApplySort(options);
```

### `.ApplyPaging(options)`
Applies `SKIP` / `TAKE` (offset) or a keyset cursor `WHERE`.
```csharp
query = query.ApplyPaging(options);
```

### `.ApplyExpand(options)`
Applies EF Core filtered includes (e.g., `Include(x => x.Orders.Where(...))`).
```csharp
query = query.ApplyExpand(options);
```

### `.ApplySelect(options)`
Applies dynamic projection. Returns `IQueryable<object>`.
```csharp
var projected = query.ApplySelect(options);
var data = await projected.ToListAsync();
```

---

## Security & Validation

If you are using the manual pipeline instead of `FlexQueryAsync`, you **must** manually validate the options before executing them.

### `.ValidateOrThrow(execOptions)`
Throws a `QueryValidationException` if any field access violation, depth violation, or operator mismatch is found.
```csharp
options.ValidateOrThrow<User>(execOptions);
```

### `.ValidateSafe(execOptions)`
Returns a `ValidationResult`. Non-throwing — use this when you prefer structured error returns over exceptions.
```csharp
var result = options.ValidateSafe<User>(execOptions);
if (!result.IsValid)
{
    return BadRequest(result.Errors);
}
```

---

## `BuildQueryResult`

After a manual pipeline execution, use this helper to construct the standardized `QueryResult<T>` envelope.

```csharp
var total = await filteredQuery.CountAsync();
var data = await pagedQuery.ApplySelect(options).ToListAsync();

return Ok(options.BuildQueryResult(data, total));
```

---

## QueryResult Mapping

If you need to project or map the results inside a `QueryResult<T>` *after* execution (for example, mapping entities to DTOs), FlexQuery provides helper extensions to safely cast the data while preserving pagination metadata.

### `.ToProjectedQueryResult<TSource, TProjected>()`
Casts the underlying `Data` collection from one type to another.

```csharp
var entityResult = await _context.Customers.FlexQueryAsync(options);
var dtoResult = entityResult.ToProjectedQueryResult<User, UserDto>();
```

### `.ToObjectResult()` & `.ToDynamicResult()`
Casts the inner generic types to `object` or `dynamic`, which is useful for untyped serialization or dynamic projection results.

```csharp
var objResult = entityResult.ToObjectResult();
```

---

## Dapper Extensions (`DbConnection`)

If you are using the `FlexQuery.NET.Dapper` provider, extension methods are provided directly on `System.Data.Common.DbConnection`.

### `.FlexQueryAsync<T>()`
Executes the full FlexQuery pipeline against an open database connection using Dapper.

```csharp
await using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync();

var result = await connection.FlexQueryAsync<User>(parameters, opts => 
{
    opts.AllowedFields = ["Id", "Name", "Email"];
});
```

## Related Topics

- [Execution Pipeline](/guide/execution-pipeline)
- [Security Governance](/guide/security-governance)
