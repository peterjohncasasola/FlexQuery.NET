> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Getting Started (v1.x Legacy)

FlexQuery.NET v1.x provides a straightforward way to add dynamic filtering, sorting, and paging to your EF Core and ASP.NET Core applications.

## Installation

```bash
dotnet add package FlexQuery.NET --version 1.7.2
```

## Basic Usage

The legacy v1.x API relies on `QueryRequest` and the `ToQueryResultAsync` extension method.

### 1. Define your Controller

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    // 1. Parse the request into options
    var options = QueryOptionsParser.Parse(request);

    // 2. Apply and execute using the extension method
    var result = await _context.Users.ToQueryResultAsync(options);

    return Ok(result);
}
```

### 2. Sample Request

```http
GET /api/users?filter=status:eq:active&sort=name:asc&page=1&pageSize=10
```

## Key Features in v1.x

- **Automatic Paging**: Returns a `QueryResult<T>` containing data and metadata (TotalCount, TotalPages, etc.).
- **DSL Filtering**: Simple `field:operator:value` syntax.
- **Fluent Extensions**: Apply filters directly to `IQueryable` using `.ApplyFilter(options)`.

---

## Moving to v2?

We highly recommend upgrading to v2 for improved security (isolation of server-side rules) and a more unified pipeline. See the [Migration Guide](/migration) for details.
