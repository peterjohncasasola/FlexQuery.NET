# Query Formats Comparison

FlexQuery supports three query formats on the same endpoint simultaneously. This guide helps you understand when and why to choose each one.

## The Formats at a Glance

| | NativeDsl | FQL | MiniOData |
|--|-----------|-----|-----------|
| **Verbosity** | Compact | Medium | Medium |
| **URL-safe** | ✅ | ⚠️ | ✅ |
| **Nested logic** | Limited | ✅ Full | ✅ Full |
| **Human-readable** | ⚠️ | ✅ | ✅ |
| **Best for** | Internal tools | Dev tools / Admin | OData migration |

---

## Filtering Examples

### NativeDsl
```
?filter=status:eq:Active
?filter=price:gt:50
?filter=name:contains:john
```
**Limitation:** Nested AND/OR groups are difficult to express. Stick to simple conditions.

### FQL
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

---

## Sorting Examples

### NativeDsl & FQL
```
?sort=Name:asc,CreatedAt:desc
?sort=Price:asc
```

### MiniOData
```
?$orderby=Name asc,CreatedAt desc
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

### Filtered includes (FQL inside parentheses)
```
?include=Orders(Status = 'Active')
?include=Orders(Status = 'Active').OrderItems(Quantity > 5)
```

### MiniOData
```
?$expand=Orders
```
MiniOData does not support filtered includes — use the standard `include` parameter with FQL inline filters for that feature.

---

## When to Use Each Format

### Use **FQL** when:
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

## Common Mistakes

| Mistake | Problem | Fix |
|---------|---------|-----|
| Sending JSON filter without encoding it | URL parser breaks the JSON | URL-encode the JSON value or use a request body |
| Mixing `$filter` and `filter` in the same request | Parser detects `$` prefix and routes to MiniOData, ignoring `filter` | Pick one convention per request |
| Assuming all formats support the same operators | NativeDsl has a smaller operator set than FQL/JSON | Check the [Operators reference](/shared/operators) |

## Related Features

- [Query Syntax](/guide/query-syntax) — Auto-detection and parser architecture
- [Filtering](/guide/filtering) — Full operator reference
- [MiniOData Adapter](/adapters/miniodata) — OData compatibility details
