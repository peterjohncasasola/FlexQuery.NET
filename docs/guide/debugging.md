# Debugging

## Overview

FlexQuery.NET includes built-in debugging tools that allow you to inspect the parsed query Abstract Syntax Tree (AST), the generated expression trees, and the execution pipeline state — without necessarily touching a database.

## Why this feature exists

When building dynamic, client-driven querying APIs, it can sometimes be difficult to determine *why* a query failed or why the generated SQL looks a certain way. Is the frontend sending malformed URL encoded strings? Is the security validation stripping out a valid field? FlexQuery provides deep introspection capabilities so you can trace the exact lifecycle of a request from the HTTP boundary down to the ADO.NET command.

## When to use

- Read this guide when you are encountering unexpected `QueryValidationExceptions`.
- Read this guide when you want to intercept and log the generated SQL strings in production.
- Use the `IFlexQueryExecutionListener` when building global APM telemetry integrations.

---

## Inspecting the Parsed AST

Before execution, you can inspect exactly what FlexQuery.NET parsed from the HTTP request by calling `.ToQueryOptions()`.

```csharp
[HttpGet("debug")]
public IActionResult DebugQuery([FromQuery] FlexQueryParameters parameters)
{
    // The internal parsed model
    QueryOptions options = parameters.ToQueryOptions();

    return Ok(new
    {
        filter = options.Filter, // The nested AND/OR tree
        sort   = options.Sort,   // Ordered list of sorts
        select = options.Select, // Projected field paths
        paging = new
        {
            page     = options.Paging.Page,
            pageSize = options.Paging.PageSize,
            skip     = options.Paging.Skip
        },
        projectionMode = options.ProjectionMode.ToString(),
        groupBy        = options.GroupBy,
        aggregates     = options.Aggregates,
        includes       = options.Expand, // v4 uses Expand
    });
}
```

**Sample output for `?filter=Status:eq:active&sort=Name:asc&page=2&pageSize=10`:**

```json
{
  "filter": {
    "logic": "And",
    "children": [
      {
        "field": "Status",
        "operator": "eq",
        "value": "active"
      }
    ]
  },
  "sort": [
    { "field": "Name", "descending": false }
  ],
  "select": null,
  "paging": {
    "page": 2,
    "pageSize": 10,
    "skip": 10
  },
  "projectionMode": "Nested",
  "groupBy": null,
  "aggregates": [],
  "includes": null
}
```

---

## Inspecting the Validation Result

If a query is failing and you want to capture the validation result before throwing an exception, you can run validation manually:

```csharp
var options = parameters.ToQueryOptions();

var execOptions = new QueryExecutionOptions
{
    AllowedFields = new HashSet<string> { "Id", "Name", "Status" }
};

var validation = options.ValidateSafe<User>(execOptions);

Console.WriteLine($"IsValid: {validation.IsValid}");
foreach (var error in validation.Errors)
{
    Console.WriteLine($"  [{error.Code}] {error.Field}: {error.Message}");
}
```

**Sample output:**

```text
IsValid: False
  [FIELD_ACCESS_DENIED] Salary: Field 'Salary' is not in the global allowed list.
  [FIELD_ACCESS_DENIED] InternalNotes: Field 'InternalNotes' is explicitly blocked.
```

---

## Diagnostic Logging (`IFlexQueryExecutionListener`)

In v4, FlexQuery introduces a formal diagnostic telemetry hook: the `IFlexQueryExecutionListener`.

You can pass a custom listener into your `options` lambda to receive real-time execution metrics.

```csharp
var result = await _db.Products.FlexQueryAsync(parameters, options =>
{
    options.AllowedFields = ["Id", "Name", "Price"];
    
    // Attach a listener for debugging
    options.Listener = new ConsoleDiagnosticsListener();
});

// Custom Listener Implementation
public class ConsoleDiagnosticsListener : IFlexQueryExecutionListener
{
    public void OnQueryExecuted(FlexQueryExecutionEvent executionEvent)
    {
        Console.WriteLine($"Query executed in {executionEvent.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total Records Found: {executionEvent.TotalCount}");
        
        if (executionEvent.Exception != null)
        {
            Console.WriteLine($"Failed with: {executionEvent.Exception.Message}");
        }
    }
}
```

---

## Viewing Generated SQL (EF Core)

If you are using the EF Core provider, you can use EF Core's built-in logging to see the SQL FlexQuery.NET generates:

```csharp
// In Program.cs or DbContext OnConfiguring
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
```

Or you can use `ToQueryString()` in the manual pipeline to inspect the SQL without executing:

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable();

query = query.ApplyFilter(options);
query = query.ApplySort(options);

// Inspect SQL without executing
var sql = query.ToQueryString();
Console.WriteLine(sql);
```

**Sample output:**

```sql
SELECT [u].[Id], [u].[Name], [u].[Email], [u].[Status]
FROM [Users] AS [u]
WHERE [u].[Status] = N'active'
ORDER BY [u].[Name]
```

---

## Common Debug Scenarios

### "My filter isn't working"

1. Check `options.Filter` in the parsed AST — is it null? The format might not have been recognized, or a syntax issue occurred (e.g., using `AND`/`OR` keywords instead of the old `&`/`|` symbols).
2. Check the operator — typos in operator strings are silently ignored during parsing but caught during validation. 

### "Results are empty but shouldn't be"

1. Verify case sensitivity: `options.CaseInsensitive` is `true` by default, but if you turned it off, "Alice" and "alice" will not match.
2. Check if a server-side pre-filter is excluding results before FlexQuery runs (e.g., `_context.Customers.Where(c => c.CustomerId == 1).FlexQueryAsync(...)`).
3. Use `query.ToQueryString()` or SQL Profiler to see the exact SQL generated.

### "Validation is rejecting a valid field"

1. Check `AllowedFields`.
2. Remember that if `StrictFieldValidation` is true, the query will completely abort on the first infraction. Check your frontend network tab to see if the UI is requesting a deeply nested relationship field that exceeds your `MaxFieldDepth` setting.
