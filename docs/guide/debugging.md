# Debugging

FlexQuery.NET includes built-in debugging tools that let you inspect the parsed query AST, generated expression trees, and execution pipeline state — without touching a database.

---

## What You Can Debug

- The parsed filter AST (Abstract Syntax Tree)
- The normalized filter form (used for caching)
- The generated LINQ expression as a string
- Paging, sort, and projection state

---

## Inspecting the Parsed AST

After parsing, the `QueryOptions.Ast` property holds the raw parsed AST (if using DSL or JQL format):

```csharp
var options = QueryOptionsParser.Parse(parameters);

// The raw parser output (JQL or DSL AST node)
Console.WriteLine(options.Ast);

// The resolved FilterGroup tree
Console.WriteLine(options.Filter?.ToString());
```

---

## Inspecting QueryOptions in a Controller

Add a debug endpoint to inspect what FlexQuery.NET parsed from a request:

```csharp
[HttpGet("debug")]
public IActionResult DebugQuery([FromQuery] FlexQueryParameters parameters)
{
    var options = QueryOptionsParser.Parse(parameters);

    return Ok(new
    {
        filter = options.Filter,
        sort   = options.Sort,
        select = options.Select,
        paging = new
        {
            page     = options.Paging.Page,
            pageSize = options.Paging.PageSize,
            skip     = options.Paging.Skip
        },
        projectionMode = options.ProjectionMode.ToString(),
        groupBy        = options.GroupBy,
        aggregates     = options.Aggregates,
        includes       = options.Includes,
        caseInsensitive = options.CaseInsensitive
    });
}
```

**Sample output for `?filter=status:eq:active&sort=name:asc&page=2&pageSize=10`:**

```json
{
  "filter": {
    "logic": "And",
    "children": [
      {
        "field": "status",
        "operator": "eq",
        "value": "active"
      }
    ]
  },
  "sort": [
    { "field": "name", "descending": false }
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
  "includes": null,
  "caseInsensitive": true
}
```

---

## Inspecting the Validation Result

Capture the validation result before throwing:

```csharp
var options = QueryOptionsParser.Parse(parameters);
var execOptions = new QueryExecutionOptions
{
    AllowedFields = new HashSet<string> { "id", "name", "status" }
};

var validation = options.ValidateSafe<User>(execOptions);

Console.WriteLine($"IsValid: {validation.IsValid}");
foreach (var error in validation.Errors)
{
    Console.WriteLine($"  [{error.Code}] {error.Field}: {error.Message}");
}
```

**Sample output:**

```
IsValid: False
  [FIELD_ACCESS_DENIED] salary: Field 'salary' is not in the global allowed list.
  [FIELD_ACCESS_DENIED] internalNotes: Field 'internalNotes' is explicitly blocked.
```

---

## Inspecting the Filter Normalizer

The `FilterNormalizer` canonicalizes a filter AST. Use it to verify cache key stability:

```csharp
using FlexQuery.NET.Builders;

var options1 = QueryOptionsParser.Parse(new FlexQueryParameters
{
    Filter = "status:eq:active,age:gte:18"
});

var options2 = QueryOptionsParser.Parse(new FlexQueryParameters
{
    Filter = "age:gte:18,status:eq:active"
});

var key1 = options1.GetCacheKey(typeof(User), "predicate");
var key2 = options2.GetCacheKey(typeof(User), "predicate");

Console.WriteLine(key1 == key2); // true — normalized form is identical
```

---

## Viewing the Cache Key

```csharp
var cacheKey = options.GetCacheKey(typeof(User), "predicate");
Console.WriteLine(cacheKey);
// e.g., "predicate:MyApp.Models.User:ci:a3f8c2d1|1|20|name_asc|id,name"

var hash = options.GetQueryHash();
Console.WriteLine(hash);
// e.g., "SHA256: 3a7f2c..."
```

---

## Logging Integration

For production debugging, log the parsed options:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers(
    [FromQuery] FlexQueryParameters parameters,
    ILogger<UsersController> logger)
{
    var options = QueryOptionsParser.Parse(parameters);

    logger.LogDebug(
        "FlexQuery parsed: filter={Filter}, sort={Sort}, page={Page}, pageSize={PageSize}",
        options.Filter != null ? "present" : "none",
        options.Sort.Count,
        options.Paging.Page,
        options.Paging.PageSize);

    // ... rest of pipeline
}
```

---

## Viewing Generated SQL (EF Core)

Use EF Core's built-in logging to see the SQL FlexQuery.NET generates:

```csharp
// In Program.cs or DbContext OnConfiguring
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
```

Or use `ToQueryString()` to inspect the SQL without executing:

```csharp
var options = QueryOptionsParser.Parse(parameters);
var query = _context.Users.AsQueryable();
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

1. Check `options.Filter` — is it null? The format might not have been recognized.
2. Try JSON format as the most explicit: `filter={"logic":"and","filters":[...]}`
3. Check the operator — typos are silently ignored. Use `FilterOperators.Normalize("myop")` to verify.

### "Results are empty but shouldn't be"

1. Verify case sensitivity: `options.CaseInsensitive` is `true` by default.
2. Check if a server-side pre-filter is excluding results before FlexQuery runs.
3. Use `query.ToQueryString()` to see the exact SQL.

### "I'm getting a double-filter in SQL"

You are using a deprecated v1 method pattern. See [Execution Pipeline](/guide/execution) for the correct approach.

### "Validation is rejecting a valid field"

Check `AllowedFields`. Field matching is **case-insensitive** by default. If you set `AllowedFields = { "Name" }` and the client sends `filter=name:eq:alice`, it will still pass.
