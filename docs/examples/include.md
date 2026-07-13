# Include & Shaping Examples

These examples demonstrate the "Dual Pipeline" feature where you can independently filter root entities and shape their nested relationships.

## Include-Level Filtering

All customers, but only show their cancelled orders.

> 💡 Alice appears even with an empty `orders` array. The `include` filter shapes the collection — it does not drop root entities.

### Request
```http
GET /api/customers?include=orders(status = "Cancelled")
```

### Response
```json
{
  "data": [
    {
      "id": 1,
      "name": "Alice Smith",
      "orders": []
    },
    {
      "id": 3,
      "name": "Charlie Brown",
      "orders": [
        { "id": 201, "status": "Cancelled", "total": 799.99 }
      ]
    }
  ]
}
```

---

## Root Filter + Include Filter Combined

Only customers named "Connelly", showing only their cancelled orders with a specific item.

### Request
```http
GET /api/customers
  ?filter=name CONTAINS "Connelly"
  &include=orders(status = "cancelled").items(sku = "Tasty Metal Pants")
```

---

## Include-Level Projection

Load customers with only selected order fields (no over-fetching).

> 💡 By selecting specific fields from the navigation property (e.g. `orders.id`), the library automatically optimizes the SQL to only fetch those columns.

### Request
```http
GET /api/customers
  ?include=orders
  &select=id,name,orders.id,orders.status,orders.total
```

### Response
```json
{
  "data": [
    {
      "id": 1,
      "name": "Alice Smith",
      "orders": [
        { "id": 101, "status": "Shipped", "total": 150.00 }
      ]
    }
  ]
}
```

---

## Deep Three-Level Include Chain

Customers who have shipped orders containing expensive items — shape the response to show only those child records.

> 💡 The `filter` filters root entities (customers). The `include` pipeline independently shapes which child records appear. 

### Request
```http
GET /api/customers
  ?filter=orders.any(status = "Shipped" AND items.any(total > 100))
  &include=orders(status = "Shipped").items(total > 100)
  &select=id,name,orders.id,orders.total,orders.items.sku,orders.items.total
```
