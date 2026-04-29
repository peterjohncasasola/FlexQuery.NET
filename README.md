# 🚀 DynamicQueryable

**DynamicQueryable** is a lightweight, extensible .NET 8 library that enables **dynamic filtering, sorting, pagination, and projection** for `IQueryable`.

It supports multiple query formats (Generic, JSON, DSL, Syncfusion, Laravel Spatie) and is designed to integrate seamlessly with **Entity Framework Core** or any LINQ provider.

---

* ✅ Dynamic filtering (OData-lite style)
* ✅ Sorting (multi-field)
* ✅ Pagination (skip/take)
* ✅ Projection (`select` with nested properties)
* ✅ Include support (nested property projection)
* ✅ Query parameter parser
  * Generic format
  * JSON format
  * DSL format
  * Syncfusion
  * Laravel Spatie
* ✅ Fully testable and extensible

---

## 📦 Installation

```bash
dotnet add package DynamicQueryable.Extensions
```

---

## ⚡ Quick Start

### 1. Parse Query Options

Use the `QueryOptionsParser` to convert your query string parameters into a `QueryOptions` object.

```csharp
using DynamicQueryable.Parsers;

// In a controller or minimal API:
var options = QueryOptionsParser.Parse(Request.Query);
```

### 2. Apply to IQueryable

Use the extension methods provided in `DynamicQueryable.Extensions`.

```csharp
using DynamicQueryable.Extensions;

[HttpGet]
public async Task<IActionResult> Get()
{
    var options = QueryOptionsParser.Parse(Request.Query);
    
    // Simple usage (returns paged data)
    var users = await _context.Users
        .ApplyQueryOptions(options)
        .ToListAsync();

    // Or use ToQueryResult for metadata (TotalCount, Page, etc.)
    var result = _context.Users.ToQueryResult(options);
    
    return Ok(result);
}
```

---

## 🔍 Query Examples

### ✅ Filtering

```http
?filter[0].field=Name
&filter[0].operator=contains
&filter[0].value=john
```

---

### ✅ Sorting

```http
?sort[0].field=Age
&sort[0].desc=true
```

---

### ✅ Pagination

```http
?page=1&pageSize=10
```

---

### ✅ Projection

```http
?select=Id,Name,Email
```

With `Includes` and no `Select`, returns root entity scalars + included navigations:

```http
?include=Profile,Orders
```

---

### ✅ Nested Projection

```http
?select=Id,Name,Profile.Name,Orders.Total
```

---

### ✅ Include + Select

```http
?include=Profile,Orders
&select=Id,Name,Profile.Name,Orders.Total
```

---

## 🔧 Supported Operators

| Operator     | Description            | Example                          |
| ------------ | ---------------------- | -------------------------------- |
| `eq`         | Equal                  | `Name eq 'John'`                 |
| `neq`        | Not equal              | `Age neq 30`                     |
| `gt`         | Greater than           | `Age gt 18`                      |
| `gte`        | Greater than or equal  | `Age gte 18`                     |
| `lt`         | Less than              | `Age lt 60`                      |
| `lte`        | Less than or equal     | `Age lte 60`                     |
| `contains`   | String contains        | `Name contains 'jo'`             |
| `startswith` | String starts with     | `Name startswith 'Jo'`           |
| `endswith`   | String ends with       | `Name endswith 'hn'`             |
| `in`         | Value exists in a list | `Status in ['Active','Pending']` |
| `notin`      | Value does not exist in a list | `Status notin ['Inactive']` |
| `between`    | Inclusive range        | `Age between 18,60`              |
| `isnull`     | Check if value is null | `DeletedAt isnull true`          |
| `notnull`    | Check if value is not null | `DeletedAt notnull`           |

---

### 🔍 Operator Examples

#### Basic

```http
?filter[0].field=Name
&filter[0].operator=eq
&filter[0].value=John
```

#### Contains

```http
?filter[0].field=Name
&filter[0].operator=contains
&filter[0].value=jo
```

#### Range

```http
?filter[0].field=Age
&filter[0].operator=gte
&filter[0].value=18
&filter[1].field=Age
&filter[1].operator=lte
&filter[1].value=60
```

#### IN

```http
?filter[0].field=Status
&filter[0].operator=in
&filter[0].value=Active,Pending
```

#### NULL

```http
?filter[0].field=DeletedAt
&filter[0].operator=isnull
&filter[0].value=true
```

---

## 🔄 Supported Query Formats

### 🔹 Generic

```http
?filter[0].field=Name
&filter[0].operator=contains
&filter[0].value=john
```

---

### 🔹 JSON

```http
?filter={
  "logic":"and",
  "filters":[
    {"field":"Name","operator":"contains","value":"john"}
  ]
}
```

---

### DSL
 
 DSL filters are parsed through a tokenizer and AST, then converted into the same `FilterGroup` model used by all other formats. In a real URL, encode `&` as `%26` because `&` is also the query-string separator.
 
 The DSL filter string supports:
 - Comparisons: `field:operator:value`
 - Logical OR: `|`
 - Logical AND: `&` (URL-encoded as `%26` in query strings)
 - Grouping: parentheses `( ... )`
 
 For sorting, pagination, and projection, use the standard generic parameters alongside the DSL filter (e.g., `sort[0].field`, `page`, `pageSize`, `select`).
 
 ```http
 ?filter=(name:eq:john|name:eq:doe)%26age:gt:20&sort[0].field=Age&sort[0].desc=true
 ```
 
 Supported operators:
 
 ```text
 eq, neq, gt, gte, lt, lte, contains, startswith, endswith, in, notin, between, isnull, notnull
 ```
 
 Nested property paths are supported:
 
 ```http
 ?filter=orders.customer.name:contains:john
 ```
 
 Additional examples:
 
 ```http
 ?filter=status:notin:Inactive,Deleted
 ?filter=age:between:18,60
 ?filter=deletedAt:notnull
 ```

---

### 🔹 Syncfusion

#### Basic Usage

```http
?where[0][field]=Name
&where[0][operator]=contains
&where[0][value]=john
&sorted[0][name]=Age
&sorted[0][direction]=descending
&skip=0
&take=10
```

#### Multiple Conditions with Logic

You can combine multiple conditions using the `condition` parameter (`and` or `or`):

```http
?where[0][field]=City
&where[0][operator]=equal
&where[0][value]=London
&where[1][field]=Age
&where[1][operator]=greaterthanorequal
&where[1][value]=25
&condition=and
```

#### Nested Property Paths

Supports nested properties in filter conditions:

```http
?where[0][field]=Profile.Bio
&where[0][operator]=contains
&where[0][value]=developer
&where[1][field]=Status
&where[1][operator]=equal
&where[1][value]=Active
&condition=and
```

---

### 🔹 Laravel Spatie

#### Basic Usage

```http
?filter[name]=john
&filter[age]=25
&sort=-created_at
&include=roles,permissions
&fields[users]=name,email
```

#### Multiple Conditions (Implicit AND)

Multiple filters are always combined with **AND** logic:

```http
?filter[name]=Alice Johnson
&filter[status]=Active
&filter[profile.role]=Developer
```

#### Nested Grouping (New!)

Supports complex nested AND/OR filter groups:

```http
?filter[or][0][name]=john
&filter[or][1][name]=doe
```

```http
?filter[and][0][name]=john
&filter[and][1][or][0][age]=20
&filter[and][1][or][1][age]=30
```

```http
?filter[or][0][and][0][name]=john
&filter[or][0][and][1][or][0][city]=london
&filter[or][0][and][1][or][1][city]=paris
&filter[or][1][status]=active
```

#### Nested Property Paths

Supports dot notation for nested properties:

```http
?filter[profile.bio]=Developer
&filter[profile.status]=Active
&sort=-created_at
```

---

## 📦 API Methods

### `ToQueryResult<T>(options)`

Executes a query and returns `QueryResult<T>` with metadata:

```csharp
var result = _context.Users.ToQueryResult(options);
// result.Data - List<T>
// result.TotalCount - total matching records
// result.Page, result.PageSize - pagination info
```

### `ToProjectedQueryResult<T>(options)`

Executes a query and returns `QueryResult<object>` with dynamic projection:

```csharp
var result = _context.Users.ToProjectedQueryResult(options);
// result.Data - List<object> (only selected fields)
// result.TotalCount - total matching records
```

Useful for API endpoints that return shaped/dynamic responses.

### EF Core Async Versions

From `DynamicQueryable.Extensions.EFCore`:

```csharp
var result = await _context.Users
    .ToQueryResultAsync(options, cancellationToken);

var projected = await _context.Users
    .ToProjectedQueryResultAsync(options, cancellationToken);
```

---

## 🔌 Integration with BaseRepository

```csharp
public async Task<QueryResult<T>> GetPagedAsync(QueryOptions options)
{
    return _dbSet.AsQueryable().ToQueryResult(options);
}

public async Task<QueryResult<object>> GetProjectedAsync(QueryOptions options)
{
    return _dbSet.AsQueryable().ToProjectedQueryResult(options);
}
```

#### Multiple Conditions (Implicit AND)

Multiple filters are always combined with **AND** logic. Laravel Spatie format does not support explicit OR logic at the top level (unlike Syncfusion's `condition` parameter):

```http
?filter[name]=Alice Johnson
&filter[status]=Active
&filter[profile.role]=Developer
```

#### Nested Property Paths

Supports dot notation for nested properties:

```http
?filter[profile.bio]=Developer
&filter[profile.status]=Active
&sort=-created_at
```

---



### Manual Construction

You can also build `QueryOptions` manually in code:

```csharp
var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Filters = [new FilterCondition { Field = "Age", Operator = "gt", Value = "18" }]
    },
    Sort = [new SortOption { Field = "Name", Descending = false }],
    Paging = new PagingOptions { Page = 1, PageSize = 10 }
};

var query = _context.Users.ApplyQueryOptions(options);
```

---

## 🔌 ASP.NET Core Integration

Since the parser is static, you can easily wrap it in a custom `ModelBinder` or just call it directly in your base controller.

```csharp
public abstract class BaseController : ControllerBase
{
    protected QueryOptions QueryOptions => QueryOptionsParser.Parse(Request.Query);
}
```

---

## 🧪 Testing

Run tests:

```bash
dotnet test
```

Includes:

* Filtering
* Sorting
* Paging
* Projection
* Parser

---

## 🧱 Architecture

```
DynamicQueryable.Extensions
│
├── Models      (FilterCondition, SortOption, etc.)
├── Builders    (ExpressionBuilder, ProjectionBuilder)
├── Extensions  (QueryableExtensions)
├── Helpers     (SelectTreeBuilder)
└── Parsers     (QueryOptionsParser, SpatieQueryParser)
```

---

## 🔗 Integration with BaseRepository

```csharp
public async Task<QueryResult<T>> GetPagedAsync(QueryOptions options)
{
    return _dbSet.AsQueryable().ToQueryResult(options);
}
```

---

## 🚀 Roadmap

* [ ] Redis caching implementation
* [ ] Expression caching (performance)
* [ ] Field-level authorization
* [ ] GraphQL-style query support

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome.

---

## 📄 License

MIT License
