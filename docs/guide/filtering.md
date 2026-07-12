# Filtering

Filtering is the most powerful feature in FlexQuery.NET. Clients can compose complex query conditions using multiple formats, operators, and logical groups.

---

## What It Does

Filtering applies a `WHERE` clause to your `IQueryable`. It supports:

- Simple equality and range comparisons
- String pattern matching
- Null checks
- Collection predicates (`any`, `all`)
- Nested AND/OR logic groups
- Multiple input formats (DSL, FQL, MiniOData)

---

## When to Use

Use filtering when clients need to **search or narrow down results** at runtime without you writing custom code per field.

---

## When NOT to Use

- Do not expose filter without `AllowedFields` on a public API.
- Do not use for **fixed server-side conditions** — apply those in code before `ApplyFilter`.

---

## Filter Formats

FlexQuery.NET supports multiple filter syntaxes. The active syntax is configured globally via `FlexQueryCore.Configure()` and parser registration (`Fql.Register()`, `MiniOData.Register()`). The default is **DSL** (`QuerySyntax.NativeDsl`).

### DSL Format

Simple colon-delimited expressions.

```
GET /api/customers?filter=status:eq:active
GET /api/customers?filter=salary:gte:50000
GET /api/customers?filter=name:contains:alice
```

**Compound (AND — URL-encode `&` as `%26`):**

```http
GET /api/customers?filter=status:eq:active%26city:eq:'New York'
```

### FQL Format (SQL-like)

Natural language query syntax. FQL uses the same `filter` parameter as DSL but requires SQL-like expressions. To enable FQL, register the parser at startup:

```csharp
FlexQueryCore.Configure(options => options.QuerySyntax = QuerySyntax.Fql);
Fql.Register();
```

```
GET /api/customers?filter=status = "active" AND salary >= 50000
GET /api/customers?filter=(name = "alice" OR name = "bob") AND status = "active"
```


---

## All Supported Operators

| Operator | Description | Example |
| :--- | :--- | :--- |
| `eq` | Equals | `status:eq:active` |
| `neq` | Not equals | `status:neq:deleted` |
| `gt` | Greater than | `salary:gt:50000` |
| `gte` | Greater than or equal | `salary:gte:50000` |
| `lt` | Less than | `salary:lt:100000` |
| `lte` | Less than or equal | `salary:lte:100000` |
| `contains` | String contains | `name:contains:alice` |
| `startswith` | String starts with | `email:startswith:admin` |
| `endswith` | String ends with | `email:endswith:.com` |
| `like` | SQL LIKE pattern | `name:like:%ali%` |
| `isnull` | Is null | `deletedAt:isnull` |
| `isnotnull` | Is not null | `deletedAt:isnotnull` |
| `in` | Value in list | `status:in:active,pending` |
| `notin` | Value not in list | `status:notin:deleted,banned` |
| `between` | Inclusive range | `age:between:18,65` |
| `any` | Collection any | `orders:any:status:eq:shipped` |
| `all` | Collection all | `orders:all:status:eq:confirmed` |
| `count` | Collection count | `orders:count:gt:5` |

> [!TIP]
> Operators are case-insensitive and support aliases. `eq`, `equal`, `equals`, `==` all map to `eq`.

---

## C# Examples

### Manual Filter Application

```csharp
var options = parameters.ToQueryOptions();
var query = _context.Customers.AsQueryable();
var filtered = query.ApplyFilter(options);
var users = await filtered.ToListAsync();
```

### Collection Predicate (any)

Request:
```
GET /api/customers?filter=orders:any:status:eq:shipped
```

This translates to:
```csharp
_context.Customers.Where(u => u.Orders.Any(o => o.Status == "shipped"))
```

SQL:
```sql
SELECT * FROM Users u WHERE EXISTS (
  SELECT 1 FROM Orders o WHERE o.UserId = u.Id AND o.Status = 'shipped'
)
```

### Nested ANY Filter (FQL)

Request (FQL syntax):
```
GET /api/customers?filter=Orders.any(Status = "shipped" AND Total > 100)
```

### Range Filter

Request:
```
GET /api/customers?filter=salary:between:50000,150000
```

SQL:
```sql
SELECT * FROM Users WHERE Age BETWEEN 18 AND 65
```

### IN List

Request:
```
GET /api/customers?filter=status:in:active,pending,trial
```

SQL:
```sql
SELECT * FROM Users WHERE Status IN ('active', 'pending', 'trial')
```

---

## JSON Output Examples

**Request:**
```
GET /api/customers?filter=status:eq:active&page=1&pageSize=3
```

**Response:**
```json
{
  "totalCount": 48,
  "resultCount": 48,
  "page": 1,
  "pageSize": 3,
  "totalPages": 16,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen",  "status": "active" },
    { "id": 2, "name": "Bob Smith",   "status": "active" },
    { "id": 5, "name": "Carol White", "status": "active" }
  ],
  "nextCursorToken": null
}
```

---

## Common Mistakes

### ❌ Filtering without AllowedFields on a public API

```csharp
// WRONG — client can filter on any field, including sensitive ones
var result = await _context.Customers.FlexQueryAsync(parameters);
```

```csharp
// CORRECT
var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "name", "email", "status" };
});
```

### ❌ Applying a server-side filter AFTER FlexQuery filter

```csharp
// WRONG — tenant filter applied after FlexQuery, may expose cross-tenant data
var query = _context.Customers.AsQueryable();
query = query.ApplyFilter(options);
query = query.Where(u => u.TenantId == tenantId); // too late
```

```csharp
// CORRECT — always apply server-side constraints FIRST
var query = _context.Customers.Where(u => u.TenantId == tenantId);
query = query.ApplyFilter(options);
```

### ❌ Using string concatenation instead of FlexQuery operators

```csharp
// WRONG — SQL injection risk
query = query.Where($"Name.Contains(\"{userInput}\")");
```

FlexQuery.NET uses expression trees — never string concatenation.

---

## Performance Notes

- All filters are compiled into LINQ expression trees before execution.
- EF Core translates them to parameterized SQL queries.
- Collection predicates (`any`, `all`) generate `EXISTS` subqueries in SQL — efficient and server-side.
- `contains` maps to SQL `LIKE '%value%'` which **cannot use an index**. Prefer `startswith` for large tables.
- Deeply nested OR groups with `contains` can be slow — consider full-text search for those cases.
- String comparisons respect the database collation — case sensitivity is handled by the database provider.
