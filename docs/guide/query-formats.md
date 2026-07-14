# Query Formats Comparison

FlexQuery supports five query formats on the same endpoint simultaneously. This guide helps you understand when and why to choose each one.

## The Formats at a Glance

| | NativeDsl | JSON | JQL | MiniOData | Generic (Indexed) |
|--|-----------|------|-----|-----------|-------------------|
| **Verbosity** | Compact | Verbose | Medium | Medium | Very verbose |
| **URL-safe** | ✅ | Needs encoding | ⚠️ | ✅ | ✅ |
| **Nested logic** | Limited | ✅ Full | ✅ Full | ✅ Full | ❌ Flat only |
| **Human-readable** | ⚠️ | ❌ | ✅ | ✅ | ❌ |
| **Best for** | Internal tools | SPAs / JSON APIs | Dev tools / Admin | OData migration | HTML forms |

---

## Filtering Examples

### NativeDsl
```
?filter=status:eq:Active
?filter=price:gt:50
?filter=name:contains:john
```
**Limitation:** Nested AND/OR groups are difficult to express. Stick to simple conditions.

### JSON
```
?filter={"logic":"and","filters":[
  {"field":"Status","operator":"eq","value":"Active"},
  {"field":"Price","operator":"gt","value":50}
]}
```
Full support for nested groups with mixed `and`/`or` logic:
```json
{
  "logic": "or",
  "groups": [
    {
      "logic": "and",
      "filters": [
        { "field": "Category", "operator": "eq", "value": "Electronics" },
        { "field": "Price", "operator": "lt", "value": 1000 }
      ]
    },
    { "field": "IsFeatured", "operator": "eq", "value": true }
  ]
}
```

### JQL
```
?filter=Status = 'Active' AND Price > 50
?filter=(Category = 'Electronics' AND Price < 1000) OR IsFeatured = true
?filter=Name CONTAINS 'pro' AND Stock > 0
```
Most human-readable. Ideal for developer tools, admin panels, and internal APIs.

### MiniOData
```
?$filter=Status eq 'Active' and Price gt 50
?$filter=contains(Name, 'pro') and Stock gt 0
?$filter=(Category eq 'Electronics' and Price lt 1000) or IsFeatured eq true
```

### Generic (Indexed)
```
?filter[0].field=Status&filter[0].operator=eq&filter[0].value=Active
?filter[1].field=Price&filter[1].operator=gt&filter[1].value=50
```
No nested logic support. All conditions are implicitly ANDed together.

---

## Sorting Examples

### NativeDsl & JQL
```
?sort=Name:asc,CreatedAt:desc
?sort=Price:asc
```

### MiniOData
```
?$orderby=Name asc,CreatedAt desc
```

### Generic
```
?sort[0].field=Name&sort[0].dir=asc
?sort[1].field=CreatedAt&sort[1].dir=desc
```

### JSON (via `sort` parameter)
```
?sort=Name:asc,CreatedAt:desc
```
Sorting is always expressed in the same `field:direction` format regardless of the filter syntax used.

---

## Grouping Examples

Grouping uses the `groupBy` parameter across all syntax types:

```
?groupBy=Category
?groupBy=Category,Region
```

Combined with aggregates (always expressed as query parameters, not inside the format):
```
?groupBy=Category&aggregate=sum(Price),count(Id)
```

---

## Relationship / Include Examples

### Simple includes (all formats)
```
?include=Orders
?include=Orders,Profile
```

### Filtered includes (JQL inside parentheses)
```
?include=Orders(Status = 'Active')
?include=Orders(Status = 'Active').OrderItems(Quantity > 5)
```

### MiniOData
```
?$expand=Orders
```
MiniOData does not support filtered includes — use the standard `include` parameter with JQL inline filters for that feature.

---

## When to Use Each Format

### Use **JSON** when:
- Your frontend sends filter payloads from a query builder UI (e.g., React Query Builder)
- You need deeply nested AND/OR logic
- The filter is constructed programmatically, not typed by a user

### Use **JQL** when:
- You're building admin panels or internal tools where developers write filters
- You want queries that are readable in logs and debugging tools
- Your API is consumed by developers who understand query languages

### Use **NativeDsl** when:
- URL compactness is critical
- Simple equality/comparison filters are sufficient
- You're building URL-sharing features in your app

### Use **MiniOData** when:
- You have existing clients that send OData `$filter` / `$orderby` parameters
- You are migrating from `Microsoft.AspNetCore.OData`
- You need backwards compatibility without rewriting client code

### Use **Generic (Indexed)** when:
- Your forms serialize query parameters using `filter[0].field` notation
- You use a form library that produces indexed query strings automatically

---

## Common Mistakes

| Mistake | Problem | Fix |
|---------|---------|-----|
| Sending JSON filter without encoding it | URL parser breaks the JSON | URL-encode the JSON value or use a request body |
| Mixing `$filter` and `filter` in the same request | Parser detects `$` prefix and routes to MiniOData, ignoring `filter` | Pick one convention per request |
| Using Indexed format for complex logic | It only supports flat AND — no OR groups | Switch to JSON or JQL for multi-group logic |
| Assuming all formats support the same operators | NativeDsl has a smaller operator set than JQL/JSON | Check the [Operators reference](/shared/operators) |

## Related Features

- [Query Syntax](/guide/query-syntax) — Auto-detection and parser architecture
- [Filtering](/guide/filtering) — Full operator reference
- [MiniOData Adapter](/adapters/miniodata) — OData compatibility details
