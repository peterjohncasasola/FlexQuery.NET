# Migration Guide: v1 → v2

FlexQuery.NET v2 is a major refactor focused on **architectural hardening, security, and a unified execution pipeline.**

---

## 🔥 Breaking Changes Summary

### 1. Configuration Architecture Redesign

v1 mixed application-wide defaults with per-request execution options in a single `QueryExecutionOptions` class. v2 introduces a two-tier configuration:

- **`FlexQueryOptions`**: Global defaults configured via DI
- **`QueryExecutionOptions`**: Per-request overrides only (nullable properties)

### 2. Separated Security from Execution Options

Properties that represent infrastructure defaults have moved to `FlexQueryOptions`:

| Property | v2 Location |
|----------|-------------|
| `MaxPageSize` | `FlexQueryOptions` (global) or `QueryExecutionOptions.MaxPageSize` (nullable override) |
| `DefaultPageSize` | `FlexQueryOptions` (global) |
| `CaseInsensitive` | `FlexQueryOptions` (global) or nullable override |
| `IncludeTotalCount` | `FlexQueryOptions` (global) or nullable override |
| `StrictFieldValidation` | `FlexQueryOptions` (global) or nullable override |
| `MaxFieldDepth` | `FlexQueryOptions` (global) or nullable override |
| `UseNoTracking` | `FlexQueryOptions` (global) or nullable override |
| `UseSplitQuery` | `QueryExecutionOptions.UseSplitQuery` (new) |

### 3. Nullable Override Semantics

Override properties in `QueryExecutionOptions` are now nullable (`T?`), following the pattern:

- `null` = use global default
- `value` = override global default

### 4. EffectiveQueryOptions is Now Internal

`EffectiveQueryOptions` is now an `internal sealed class`. It is used internally by the execution pipeline but should never appear in public APIs.

### 2. Renamed DTOs & Types
- `QueryRequest` → **Deprecated** (Use `FlexQueryParameters`)
- `FlexQueryRequest` → **Deprecated** (Use `FlexQueryParameters`)
- `SortOption` → **`SortNode`** (Aligned with the new AST-based architecture)

---

## Configuration Migration

```csharp
var exec = new QueryExecutionOptions
{
    MaxPageSize = 1000,
    DefaultPageSize = 50,
    CaseInsensitive = true,
    IncludeTotalCount = true,
    StrictFieldValidation = true,
    MaxFieldDepth = 5,
    UseNoTracking = true,
    AllowedFields = new HashSet<string> { "id", "name", "email" }
};
```

**Global configuration in Program.cs:**

```csharp
builder.Services.AddFlexQuery(options =>
{
    options.MaxPageSize = 1000;
    options.DefaultPageSize = 50;
    options.CaseInsensitive = true;
    options.IncludeTotalCount = true;
    options.StrictFieldValidation = true;
    options.MaxFieldDepth = 5;
    options.UseNoTracking = true;
});
```

**Per-request override in endpoint:**

```csharp
exec.MaxPageSize = 100;      // Override global MaxPageSize
exec.MaxFieldDepth = 2;      // Override global MaxFieldDepth
// Security lists remain per-request
exec.AllowedFields = new HashSet<string> { "id", "name", "email" };
```

---

## Renamed Types

`SortOption` was renamed to `SortNode`
to align with the new query composition architecture introduced in v2.

| v1 API | v2 API |
| :--- | :--- |
| `SortOption` | `SortNode` |

### Before (v1.x)

```csharp
var options = new QueryOptions
{
    Sort = new List<SortOption>
    {
        new SortOption
        {
            Field = "Name",
            Descending = false
        }
    }
};
```

### After (v2.0)
```csharp
var options = new QueryOptions
{
    Sort = new List<SortNode>
    {
        new SortNode
        {
            Field = "Name",
            Descending = false
        }
    }
};
```

---

## 🆚 Execution Pipeline Comparison

### ❌ Legacy Approach (v1.x)
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    var options = QueryOptionsParser.Parse(request);
    
    // Security rules mixed with client input
    options.AllowedFields = ["Id", "Name"]; 

    var result = await _context.Users
        .ApplyValidatedQueryOptions(options)
        .ToQueryResultAsync();

    return Ok(result);
}
```

### ✅ Modern Approach (v2.0)
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    // Single entry point: Parsing, validation, and execution
    var result = await _context.Users.FlexQueryAsync(parameters, options => 
    {
        // Trusted server-side configuration
        options.AllowedFields = ["Id", "Name"];
    });

    return Ok(result);
}
```

---

## 🛠️ Deprecated APIs List

The following APIs are marked as `[Obsolete]` in v2.0. They will continue to work but will be removed in v3.0.

| Deprecated API | Replacement | Reason |
|----------------|-------------|--------|
| `SortOption` | `SortNode` | Part of the AST node standardization. |
| `QueryRequest` | `FlexQueryParameters` | Renamed for clarity and Swagger support. |
| `ApplyValidatedQueryOptions` | `FlexQuery(...)` | Logic moved into unified pipeline. |
| `ToQueryResultAsync` | `FlexQueryAsync(...)` | Unified pipeline handles materialization. |
| `ApplySelect` | `FlexQuery(...)` | Projection is now part of the core pipeline. |
| `EffectiveQueryOptions` (public) | N/A | Now internal - used only by the execution pipeline. |
| `FlexQueryAsync(FlexQueryParameters, FlexQueryOptions)` | `FlexQueryAsync(FlexQueryParameters)` | Use DI configuration instead. |

---

## 🚀 Upgrade Steps

1. **Update Packages**: Update all `FlexQuery.NET.*` packages to version `2.0.0`.
2. **Move Global Defaults**: Move `MaxPageSize`, `DefaultPageSize`, `CaseInsensitive`, `IncludeTotalCount`, `StrictFieldValidation`, `MaxFieldDepth`, `UseNoTracking` from per-request to `AddFlexQuery` in `Program.cs`.
3. **Swap DTOs**: Replace `QueryRequest` with `FlexQueryParameters` in your controllers.
4. **Update Sorting**: Replace `SortOption` with `SortNode` in any programmatic query composition logic.
5. **Simplify Pipeline**: Replace the multi-step `Apply...` chain with a single `.FlexQuery()` or `.FlexQueryAsync()` call.
6. **Update Override Properties**: Change override properties from `T` to `T?` - use `null` to inherit from global defaults.
7. **Move Security**: Relocate your whitelists (`AllowedFields`) and blacklists into the `FlexQuery` configuration delegate.