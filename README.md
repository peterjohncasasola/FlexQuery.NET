<p align="center">
  <img src="https://raw.githubusercontent.com/peterjohncasasola/FlexQuery.NET/main/assets/logo.png" alt="FlexQuery.NET Logo" width="400">
</p>

# FlexQuery.NET

**Dynamic filtering, sorting, paging, and projection for IQueryable in .NET.**

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexQuery.NET.svg)](https://www.nuget.org/packages/FlexQuery.NET)
[![Dotnet Support](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0-blueviolet)](https://dotnet.microsoft.com/download)
[![Documentation](https://img.shields.io/badge/docs-vercel-blue.svg)](https://flexquery.vercel.app)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

FlexQuery.NET is a lightweight and powerful dynamic query engine for .NET. It allows you to transform complex API query parameters into optimized, EF Core-translatable expression trees with a single line of code.

### ⚡ Key Features

- **Dynamic Querying**: Powerful DSL, JQL, and JSON-based filtering.
- **IQueryable-Native**: 100% server-side translation—no client-side evaluation.
- **Advanced Projection**: Automatic SQL `SELECT` optimization including nested includes.
- **Governance & Security**: Built-in field-level validation and operator restrictions.
- **High Performance**: Thread-safe expression caching for ultra-low latency.
- **Explicit Joins**: SQL-like join support with alias-aware field resolution.

---

## 🚀 Quick Start

### 1. Installation

```bash
dotnet add package FlexQuery.NET
dotnet add package FlexQuery.NET.EFCore
dotnet add package FlexQuery.NET.AspNetCore
```

### 2. Simple Usage

Securely execute a dynamic query directly from your controller:

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    // One-stop shop: Parsing + Validation + Execution
    var result = await _context.Users.FlexQueryAsync(parameters, options => 
    {
        options.AllowedFields = ["Id", "Name", "Email", "Status"];
        options.AllowOperators("Status", FilterOperators.Eq, FilterOperators.In);
    });

    return Ok(result);
}
```

### 3. Example Request

```http
GET /api/users?filter=age:gt:18&sort=createdAt:desc&page=1&pageSize=20&select=id,name,email
```

---

## 📚 Documentation

For detailed guides, API references, and advanced scenarios, visit our documentation site:

👉 **[https://flexquery.vercel.app](https://flexquery.vercel.app)**

### Quick Links
- [Getting Started](https://flexquery.vercel.app/guide/getting-started)
- [Query Composition](https://flexquery.vercel.app/guide/composition)
- [Explicit Joins](https://flexquery.vercel.app/guide/joins)
- [Governance & Security](https://flexquery.vercel.app/guide/security)
- [Performance Optimization](https://flexquery.vercel.app/guide/performance)
- [Migration Guide (v1 → v2)](https://flexquery.vercel.app/migration/v1-to-v2)

---

## 📄 License

FlexQuery.NET is licensed under the [MIT License](LICENSE).
