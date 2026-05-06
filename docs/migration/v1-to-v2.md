# Migration Guide: v1 → v2

FlexQuery.NET v2 is a major refactor focused on **architectural hardening, security, and a unified execution pipeline.**

---

## 🔥 Breaking Changes Summary

### 1. Separation of Concerns
In v1, security rules (like `AllowedFields`) were part of the query model. In v2, these are strictly moved to `QueryExecutionOptions` (trusted server rules).

### 2. Renamed DTOs & Types
- `QueryRequest` → **Deprecated** (Use `FlexQueryParameters`)
- `FlexQueryRequest` → **Deprecated** (Use `FlexQueryParameters`)
- `SortOption` → **`SortNode`** (Aligned with the new AST-based architecture)

### 3. Unified Pipeline
Individual extension methods like `ApplyValidatedQueryOptions`, `ToQueryResultAsync`, and `ApplySelect` are now internal steps of the unified `FlexQuery` method.

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
| :--- | :--- | :--- |
| `SortOption` | `SortNode` | Part of the AST node standardization. |
| `QueryRequest` | `FlexQueryParameters` | Renamed for clarity and Swagger support. |
| `ApplyValidatedQueryOptions` | `FlexQuery(...)` | Logic moved into unified pipeline. |
| `ToQueryResultAsync` | `FlexQueryAsync(...)` | Unified pipeline handles materialization. |
| `ApplySelect` | `FlexQuery(...)` | Projection is now part of the core pipeline. |

---

## 🚀 Upgrade Steps

1. **Update Packages**: Update all `FlexQuery.NET.*` packages to version `2.0.0`.
2. **Swap DTOs**: Replace `QueryRequest` with `FlexQueryParameters` in your controllers.
3. **Update Sorting**: Replace `SortOption` with `SortNode` in any programmatic query composition logic.
4. **Simplify Pipeline**: Replace the multi-step `Apply...` chain with a single `.FlexQuery()` or `.FlexQueryAsync()` call.
5. **Move Security**: Relocate your whitelists (`AllowedFields`) and blacklists into the `FlexQuery` configuration delegate.
