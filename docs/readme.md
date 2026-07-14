# 🚀 FlexQuery.NET

**Dynamic querying for .NET made simple, secure, and performant.**

FlexQuery.NET is a lightweight library for `IQueryable` that enables dynamic filtering, sorting, projection, and pagination via URL parameters. It serves as a modern alternative to OData and GraphQL for developers who want to stay within the REST paradigm.

```csharp
// Single unified call: Parse -> Validate -> Execute
var result = await _context.Users.FlexQueryAsync(parameters, options => 
{
    options.AllowedFields = ["Id", "Name", "Orders.Status"];
    options.MaxFieldDepth = 2;
});
```

---

## 📖 Documentation

- [Getting Started](/guide/getting-started)
- [Basic Usage Guide](/guide/basic-usage)
- [Security & Validation](/guide/security)
- [Migration Guide (v1 → v2)](/migration)
- [Comparison: FlexQuery vs GraphQL vs OData](/guide/comparison)

---

## ⚡ Performance & Optimization (v2.1)

FlexQuery.NET v2.1 introduces server-side **Execution Strategies**, including support for EF Core **Split Queries** and automatic **No-Tracking** execution to ensure maximum performance for complex data graphs.

```csharp
var result = await _context.Users.FlexQueryAsync(parameters, exec => 
{
    exec.UseSplitQuery = true; // Avoid cartesian explosion
    exec.UseNoTracking = true; // Enabled by default
});
```

> [!TIP]
> If you are upgrading from v1.x, please check the [Migration Guide](/migration) for breaking changes and deprecated APIs.

---

## 🚀 Quick Start

1. **Install**
   ```bash
   dotnet add package FlexQuery.NET.EntityFrameworkCore
   ```

2. **Controller Integration**
   ```csharp
   [HttpGet]
   public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
   {
       return Ok(await _context.Users.FlexQueryAsync(parameters));
   }
   ```

3. **Query**
   `GET /api/users?filter=Name:contains:John&sort=CreatedAt:desc&select=Id,Name`

---

## ⚖️ License
MIT License. Created by FlexQuery.NET Contributors.
