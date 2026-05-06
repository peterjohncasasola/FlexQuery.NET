> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Flattened Projections

By default, FlexQuery.NET preserves the original object hierarchy. However, for reporting or integration with legacy systems, you might need to flatten nested collections into a linear rowset.

## Flat Mode (`mode=flat`)

Linearizes a single navigation path into a flat list of leaf objects.

**Query:**
`?select=orders.orderItems.productName as product,orders.orderItems.quantity as qty&mode=flat`

**Output:**
```json
[
  { "product": "Laptop", "qty": 1 },
  { "product": "Mouse", "qty": 2 }
]
```

**Generated LINQ:**
The library builds a sequential `SelectMany` chain:
```csharp
query.SelectMany(c => c.Orders)
     .SelectMany(o => o.OrderItems)
     .Select(oi => new { product = oi.ProductName, qty = oi.Quantity })
```

> [!NOTE]
> **Constraint**: `mode=flat` requires a single linear navigation path. Branching into multiple collections will trigger a validation error.

## Flat-Mixed Mode (`mode=flat-mixed`)

Flattens root entity fields alongside deeply nested collection fields into a single rowset, preserving parent context.

**Query:**
`?select=id as customerId,name,orders.status as orderStatus,orders.orderItems.productName as product&mode=flat-mixed`

**Output:**
```json
[
  { "customerId": 1, "name": "Alice", "orderStatus": "Shipped", "product": "Laptop" },
  { "customerId": 1, "name": "Alice", "orderStatus": "Shipped", "product": "Mouse" }
]
```

**Generated LINQ:**
The library carries context through the chain using correlated `SelectMany` projections:
```csharp
query.SelectMany(c => c.Orders, (c, o) => new { c, o })
     .SelectMany(x => x.o.OrderItems, (x, oi) => new {
         customerId = x.c.Id,
         name = x.c.Name,
         orderStatus = x.o.Status,
         product = oi.ProductName
     })
```

This mode allows you to "join" all levels of a hierarchy into a flat result set while maintaining full EF Core server-side translation.

