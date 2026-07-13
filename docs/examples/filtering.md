# Collection & Logical Filtering Examples

These examples demonstrate complex condition groupings and collection evaluation logic.

## Multi-Condition (AND/OR)

Customers from Berlin or Paris, sorted by name.

### Request
```http
GET /api/customers?filter=(city = "Berlin" OR city = "Paris") AND status = "Active"&sort=name asc
```

### Response
```json
{
  "data": [
    { "id": 5,  "name": "Anna Bauer",    "city": "Berlin", "status": "Active" },
    { "id": 12, "name": "Claude Dupont", "city": "Paris",  "status": "Active" }
  ]
}
```

---

## Nested `ANY` Query

Customers who have *at least one* order over £500.

### Request
```http
GET /api/customers?filter=orders.any(total > 500)
```

### LINQ Translation
```csharp
query.Where(c => c.Orders.Any(o => o.Total > 500))
```

---

## Scoped Collection Filter (Same-Element Constraint)

Customers with a single order that is BOTH cancelled AND over £500.

> 💡 Without scoped syntax, `orders.status = "Cancelled" AND orders.total > 500` would return customers who have *any* cancelled order AND *any* order over £500 — potentially different orders.

### Request
```http
GET /api/customers?filter=orders.any(status = "Cancelled" AND total > 500)
```

### LINQ Translation
```csharp
query.Where(c => c.Orders.Any(o => o.Status == "Cancelled" && o.Total > 500))
```

---

## Multi-Level Nested `ANY`

Customers with a cancelled order that contains a specific order item.

### Request
```http
GET /api/customers?filter=orders.any(status = "Cancelled" AND items.any(sku CONTAINS "Laptop"))
```

---

## `ALL` Quantifier

Customers where ALL orders are completed.

> 💡 `all(...)` uses vacuous truth — customers with no orders also satisfy the condition.

### Request
```http
GET /api/customers?filter=orders.all(status = "Shipped" OR status = "Delivered")
```

### LINQ Translation
```csharp
query.Where(c => c.Orders == null || c.Orders.All(o => o.Status == "Shipped" || o.Status == "Delivered"))
```

---

## IS NULL / IS NOT NULL

Customers without an assigned address, or with a specific nested null check.

### Request
```http
GET /api/customers?filter=address isnull
GET /api/customers?filter=address.city notnull
```

---

## COUNT on Collection

Customers with more than 3 orders.

### Request
```http
GET /api/customers?filter=orders count > 3
```

---

## BETWEEN Range Filter

Orders placed in Q1 2026.

### Request
```http
GET /api/orders?filter=orderDate between "2026-01-01","2026-03-31"
```
