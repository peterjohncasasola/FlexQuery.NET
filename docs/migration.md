# Migration Guide: v1 → v2

This guide helps you upgrade FlexQuery.NET v1.x to the modern v2.x API. The changes are focused on **safety**, **separation of concerns**, and **simplicity**.

---

## What Changed at a Glance

| Aspect | v1.x (Legacy) | v2.x (Current) |
| :--- | :--- | :--- |
| **Input DTO** | `QueryRequest` or `FlexQueryRequest` (Obsolete in v2, will be removed in v3) | `FlexQueryParameters` |
| **Security rules** | Set on `QueryOptions` (mixed with client input) | Set in `FlexQueryAsync` delegate (server-side, isolated) |
| **Main execution** | `ApplyValidatedQueryOptions` → `ToQueryResultAsync` | `FlexQueryAsync` (unified) |
| **Projection** | `ToProjectedQueryResultAsync` | `FlexQueryAsync` (automatic) |
| **Validation** | Mixed into `ApplyValidatedQueryOptions` | `ValidateOrThrow<T>` / `ValidateSafe<T>` |
| **Includes** | `ApplyFilteredIncludes` (manual) | `FlexQueryAsync` (automatic) |

> [!WARNING]
> All v1 APIs are marked `[Obsolete]` and hidden from IntelliSense in v2. They will be **removed in v3**.

---

## Before vs. After

### v1 Pattern

```csharp
// ❌ v1 — Security mixed with client input, manual pipeline

[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] QueryRequest request)
{
    var options = QueryOptionsParser.Parse(request);

    // BAD: security rules set on the same object as client input
    options.AllowedFields = new[] { "Id", "Name", "Email" };

    var result = await _context.Users
        .ApplyValidatedQueryOptions(options)  // applies filter AND validates
        .ToProjectedQueryResultAsync(options); // applies filter AGAIN — double filter!

    return Ok(result);
}
```

**Problems with v1:**
- Security rules are on `QueryOptions` — the same object the client controls.
- `ApplyValidatedQueryOptions` + `ToProjectedQueryResultAsync` causes double filtering.
- Hard to test, no clean separation between input parsing and server configuration.

---

### v2 Pattern

```csharp
// ✅ v2 — Clean separation, unified pipeline, no double filtering

[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        // Security rules are server-side only — client cannot influence these
        exec.AllowedFields    = new HashSet<string> { "id", "name", "email" };
        exec.BlockedFields    = new HashSet<string> { "passwordHash" };
        exec.MaxFieldDepth    = 2;
    });

    return Ok(result);
}
```

---

## Step-by-Step Upgrade

### Step 1: Update NuGet Packages

```bash
dotnet add package FlexQuery.NET
dotnet add package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.AspNetCore
```

### Step 2: Replace `QueryRequest` with `FlexQueryParameters`

```csharp
// Before
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)

// After
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
```

### Step 3: Replace `QueryOptionsParser.Parse(QueryRequest)` 

```csharp
// Before
var options = QueryOptionsParser.Parse(request);

// After
var options = QueryOptionsParser.Parse(parameters);
```

### Step 4: Replace the Execution Chain

```csharp
// Before (causes double filtering)
var result = await _context.Users
    .ApplyValidatedQueryOptions(options)
    .ToProjectedQueryResultAsync(options);

// After (single pass, unified)
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
});
```

### Step 5: Move Security Rules to the Delegate

Security rules **must** be defined in the `FlexQueryAsync` configuration delegate — not on `QueryOptions`.

```csharp
// Before (v1 — insecure pattern)
options.AllowedFields = new[] { "Name", "Email" };

// After (v2 — correct isolation)
exec.AllowedFields = new HashSet<string> { "name", "email" };
```

### Step 6: Update Validation

```csharp
// Before
var validation = query.Validate(options);

// After
var validation = options.ValidateSafe<User>(execOptions);

// Or throw on failure
options.ValidateOrThrow<User>(execOptions);
```

---

## Deprecated APIs Reference

| Deprecated API | Replacement | Removed In |
| :--- | :--- | :--- |
| `QueryRequest` | `FlexQueryParameters` | v3 |
| `QueryOptionsParser.Parse(QueryRequest)` | `QueryOptionsParser.Parse(FlexQueryParameters)` | v3 |
| `ApplyValidatedQueryOptions` | Manual `ValidateOrThrow<T>` + `QueryBuilder.Apply` | v3 |
| `ToQueryResultAsync` | `FlexQueryAsync` | v3 |
| `ToProjectedQueryResultAsync` | `FlexQueryAsync` | v3 |
| `query.Validate(options)` | `options.ValidateSafe<T>(execOptions)` | v3 |

---

## Common Migration Pitfalls

### ❌ Still setting AllowedFields on QueryOptions

```csharp
// WRONG — this field does not exist on QueryOptions in v2
var options = QueryOptionsParser.Parse(parameters);
options.AllowedFields = ...; // ← compile error in v2
```

```csharp
// CORRECT — set it in the FlexQueryAsync delegate
await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "id", "name" };
});
```

### ❌ Combining ApplyValidatedQueryOptions with FlexQueryAsync

```csharp
// WRONG — double filtering
var query = _context.Users.AsQueryable();
var query = query.ApplyValidatedQueryOptions(options);
var result = await query.FlexQueryAsync<User>(parameters, exec => { ... });
```

Use one or the other, not both.

### ❌ Using ToProjectedQueryResultAsync after ApplyValidatedQueryOptions

```csharp
// WRONG — double filter (both methods apply the filter internally)
var query = _context.Users.AsQueryable();
var query = query.ApplyValidatedQueryOptions(options);
var result = await query.ToProjectedQueryResultAsync(options);
```

```csharp
// CORRECT — use FlexQueryAsync which handles everything once
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec => { ... });
```

---

## Backward Compatibility

All v1 APIs still compile and run in v2. They emit `[Obsolete]` warnings and are hidden from IntelliSense. Your existing code will continue to work — but migrate before v3 to avoid breaking changes.
