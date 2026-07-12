# Query Format Examples

FlexQuery.NET supports multiple query formats to handle both simple URL requests and complex JSON objects sent from data-grid UI builders.

---

## DSL Format (Default)

The native FlexQuery DSL uses colon-separated values. It is compact and URL-friendly.

### Simple Filter

**Request:**
```http
GET /api/customers?filter=status:eq:active&sort=name:asc
```

### Compound Filter (AND)

Multiple conditions are separated by commas (AND logic). URL-encode `&` as `%26`.

**Request:**
```http
GET /api/customers?filter=status:eq:active%26city:eq:New York
```

### OR Logic

**Request:**
```http
GET /api/customers?filter=status:eq:active|status:eq:pending
```

### Range and Set Filter

**Request:**
```http
GET /api/customers
  ?filter=(salary:between:50000,100000)%26(status:in:Active,Review)
  &sort=salary:asc
```

### Collection Predicate (ANY)

Filter customers who have at least one shipped order.

**Request:**
```http
GET /api/customers?filter=orders:any:status:eq:shipped
```

### Response

```json
{
  "totalCount": 12,
  "resultCount": 12,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen", "status": "active", "salary": 75000 },
    { "id": 2, "name": "Bob Smith", "status": "active", "salary": 82000 }
  ],
  "nextCursorToken": null
}
```

---

## FQL Format (SQL-Like)

FQL uses natural language expressions. It requires the `FlexQuery.NET.Parsers.Fql` package and `Fql.Register()` at startup.

### Simple Filter

**Request:**
```http
GET /api/customers?filter=status = "active" AND salary >= 50000
```

### Nested Logic

**Request:**
```http
GET /api/customers?filter=(name = "alice" OR name = "bob") AND status = "active"
```

### Collection Predicate

**Request:**
```http
GET /api/customers?filter=Orders.any(Status = "shipped" AND Total > 100)
```

### Sort and Project

**Request:**
```http
GET /api/customers?filter=status = "active"
  &sort=name asc
  &select=id,name,email
```

---

## JSON Format

For complex nested filters, send a JSON object. This is useful when URL encoding becomes unwieldy.

### Nested AND/OR

**Request:**
```http
GET /api/customers?filter={"logic":"and","filters":[
  {"field":"status","operator":"eq","value":"active"},
  {"logic":"or","filters":[
    {"field":"salary","operator":"gte","value":"50000"},
    {"field":"name","operator":"contains","value":"senior"}
  ]}
]}
```

Equivalent to: `status = "active" AND (salary >= 50000 OR name contains "senior")`

### Response

```json
{
  "totalCount": 8,
  "resultCount": 8,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen", "status": "active", "salary": 75000 },
    { "id": 3, "name": "Senior Advisor", "status": "active", "salary": 90000 }
  ],
  "nextCursorToken": null
}
```

---

## MiniOData Format

MiniOData uses standard OData `$filter`, `$orderby`, `$select`, and `$expand` parameters. It requires the `FlexQuery.NET.Parsers.MiniOData` package and `MiniOData.Register()` at startup.

### Filter and Sort

**Request:**
```http
GET /api/customers?$filter=salary ge 50000 and status eq 'active'
  &$orderby=name asc
```

### Select and Expand

**Request:**
```http
GET /api/customers?$select=id,name,email
  &$expand=orders
```

### Top and Skip

**Request:**
```http
GET /api/customers?$top=20&$skip=40
```

### Count

**Request:**
```http
GET /api/customers?$filter=status eq 'active'&$count=true
```

---

## Format Comparison

| Feature | DSL | FQL | JSON | MiniOData |
| :--- | :--- | :--- | :--- | :--- |
| **URL-friendly** | ✅ Very | ⚠️ Needs encoding | ⚠️ Needs encoding | ✅ Good |
| **Human-readable** | Medium | ✅ High | Medium | ✅ High |
| **Nested logic** | Limited | ✅ Full | ✅ Full | ✅ Full |
| **Package required** | Core (default) | `FlexQuery.NET.Parsers.Fql` | Core (default) | `FlexQuery.NET.Parsers.MiniOData` |
| **Registration** | None | `Fql.Register()` | None | `MiniOData.Register()` |
| **Best for** | Internal tools | Developer tools | Complex conditions | OData migration |

---

## Auto-Detection

When using the JSON or DSL format, FlexQuery.NET auto-detects the input format:

1. **`$` prefix on parameter keys** — If any raw query parameter starts with `$` (e.g., `$filter`, `$orderby`), the MiniOData parser claims the request.
2. **JSON object** — If the `filter` parameter is a JSON object (starts with `{`), the JSON parser is used.
3. **DSL/FQL** — Otherwise, the configured default parser (DSL or FQL) handles the request.

This means your API can seamlessly accept multiple formats on the **same endpoint** without structural changes.

---

## Related Topics

- [Query Syntax](/guide/query-syntax) — How to configure and register parsers
- [Filtering](/guide/filtering) — All supported operators and filter patterns
- [Query Language Reference](/shared/query-language) — Complete syntax reference
