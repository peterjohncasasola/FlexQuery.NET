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
- Multiple input formats (DSL, JQL, JSON, Indexed)

---

## When to Use

Use filtering when clients need to **search or narrow down results** at runtime without you writing custom code per field.

---

## When NOT to Use

- Do not expose filter without `AllowedFields` on a public API.
- Do not use for **fixed server-side conditions** — apply those in code before `ApplyFilter`.

---

## Filter Formats

FlexQuery.NET auto-detects the format from the query string.

### DSL Format

Simple colon-delimited expressions.

```
GET /api/users?filter=status:eq:active
GET /api/users?filter=age:gte:18
GET /api/users?filter=name:contains:alice
```

**Compound (AND by default):**

```
GET /api/users?filter=status:eq:active,age:gte:18
```

### JQL Format (SQL-like)

Natural language query syntax using the `query` parameter.

```
GET /api/users?query=status = "active" AND age >= 18
GET /api/users?query=(name = "alice" OR name = "bob") AND status = "active"
```

### JSON Format

Structured filter tree for complex nested logic.

```
GET /api/users?filter={"logic":"and","filters":[
  {"field":"status","operator":"eq","value":"active"},
  {"field":"age","operator":"gte","value":"18"}
]}
```

**Nested OR group:**

```json
{
  "logic": "and",
  "filters": [
    { "field": "status", "operator": "eq", "value": "active" },
    {
      "logic": "or",
      "filters": [
        { "field": "name", "operator": "contains", "value": "alice" },
        { "field": "name", "operator": "contains", "value": "bob" }
      ]
    }
  ]
}
```

### Indexed Format

Good for form-based UIs.

```
GET /api/users?filter[0].field=status&filter[0].operator=eq&filter[0].value=active
              &filter[1].field=age&filter[1].operator=gte&filter[1].value=18
              &logic=and
```

---

## All Supported Operators

| Operator | Description | Example |
| :--- | :--- | :--- |
| `eq` | Equals | `status:eq:active` |
| `neq` | Not equals | `status:neq:deleted` |
| `gt` | Greater than | `age:gt:18` |
| `gte` | Greater than or equal | `age:gte:18` |
| `lt` | Less than | `price:lt:100` |
| `lte` | Less than or equal | `price:lte:100` |
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
var options = QueryOptionsParser.Parse(parameters);
var query = _context.Users.AsQueryable();
var filtered = query.ApplyFilter(options);
var users = await filtered.ToListAsync();
```

### Collection Predicate (any)

Request:
```
GET /api/users?filter=orders:any:status:eq:shipped
```

This translates to:
```csharp
_context.Users.Where(u => u.Orders.Any(o => o.Status == "shipped"))
```

SQL:
```sql
SELECT * FROM Users u WHERE EXISTS (
  SELECT 1 FROM Orders o WHERE o.UserId = u.Id AND o.Status = 'shipped'
)
```

### Nested ANY Filter

Request:
```
GET /api/users?query=Orders.any(Status = "shipped" AND Amount > 100)
```

### Range Filter

Request:
```
GET /api/users?filter=age:between:18,65
```

SQL:
```sql
SELECT * FROM Users WHERE Age BETWEEN 18 AND 65
```

### IN List

Request:
```
GET /api/users?filter=status:in:active,pending,trial
```

SQL:
```sql
SELECT * FROM Users WHERE Status IN ('active', 'pending', 'trial')
```

---

## JSON Output Examples

**Request:**
```
GET /api/users?filter=status:eq:active&page=1&pageSize=3
```

**Response:**
```json
{
  "data": [
    { "id": 1, "name": "Alice Chen",  "status": "active" },
    { "id": 2, "name": "Bob Smith",   "status": "active" },
    { "id": 5, "name": "Carol White", "status": "active" }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 3
}
```

---

## Common Mistakes

### ❌ Filtering without AllowedFields on a public API

```csharp
// WRONG — client can filter on any field, including sensitive ones
var result = await _context.Users.FlexQueryAsync<User>(parameters);
```

```csharp
// CORRECT
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "name", "email", "status" };
});
```

### ❌ Applying a server-side filter AFTER FlexQuery filter

```csharp
// WRONG — tenant filter applied after FlexQuery, may expose cross-tenant data
var query = _context.Users.AsQueryable();
query = query.ApplyFilter(options);
query = query.Where(u => u.TenantId == tenantId); // too late
```

```csharp
// CORRECT — always apply server-side constraints FIRST
var query = _context.Users.Where(u => u.TenantId == tenantId);
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
- `CaseInsensitive = true` (default) uses database collation — no performance penalty on most providers.
