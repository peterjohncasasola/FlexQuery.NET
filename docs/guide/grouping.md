# Grouping & Aggregates

## Overview

FlexQuery.NET supports server-side GROUP BY with aggregate projections (count, sum, avg) and HAVING conditions — all driven by query parameters with no custom backend code required.

## Why this feature exists

Reporting and analytics endpoints historically require custom SQL procedures or hardcoded LINQ statements for every aggregate view ("total orders by status", "revenue by region", etc.). By exposing grouping and aggregates as a first-class query feature, FlexQuery.NET allows a single generic endpoint to power an entire report dashboard, where the frontend decides what groupings to apply dynamically.

---

## What It Does

Grouping and aggregates allow clients to:

- Group results by one or more fields
- Compute aggregates per group (count, sum, avg)
- Filter groups with a HAVING condition
- Combine with sorting and paging

---

## When to Use

Use grouping for:

- Dashboard summary endpoints
- Report generation (totals by category, region, status)
- Analytics endpoints where you return counts or sums, not raw rows

---

## When NOT to Use

- Do not use grouping for simple list endpoints — it adds unnecessary complexity.
- Do not expose groupBy without restricting which fields can be grouped on.

---

## HTTP Examples

### Count by Status

**DSL:**
```
GET /api/users?select=status&aggregates=*:count&groupBy=status
```

**FQL:**
```
GET /api/users?select=status&aggregates=COUNT(*)&groupBy=status
```

**Response:**
```json
{
  "totalCount": 3,
  "data": [
    { "status": "active",   "Count": 42 },
    { "status": "inactive", "Count": 6  },
    { "status": "pending",  "Count": 12 }
  ]
}
```

### Sum of Orders by User

**DSL:**
```
GET /api/orders?select=userId&aggregates=Amount:sum&groupBy=userId
```

**FQL:**
```
GET /api/orders?select=UserId&aggregates=SUM(Amount)&groupBy=UserId
```

**Response:**
```json
{
  "data": [
    { "userId": 1, "AmountSum": 1250.00 },
    { "userId": 2, "AmountSum": 780.50  }
  ]
}
```

### Average with HAVING

**DSL:**
```
GET /api/orders?select=customerId&aggregates=Amount:avg&groupBy=customerId&having=avg(Amount):gt:500
```

**FQL:**
```
GET /api/orders?select=CustomerId&aggregates=AVG(Amount)&groupBy=CustomerId&having=AVG(Amount) > 500
```

Only returns groups where the average order amount exceeds 500.

**Response:**
```json
{
  "data": [
    { "customerId": 1, "AmountAvg": 625.00 },
    { "customerId": 5, "AmountAvg": 812.50 }
  ]
}
```

### Sort by Aggregate

**DSL:**
```
GET /api/users?select=status&aggregates=*:count&groupBy=status&sort=Count:desc
```

**FQL:**
```
GET /api/users?select=status&aggregates=COUNT(*)&groupBy=status&sort=Count DESC
```

---

## Aggregate Syntax

Aggregates are specified in the dedicated `aggregates` parameter, not in `select`.

**DSL format:**
```
aggregates=Field:Function[:Alias]
```

| DSL Example | Alias Generated | Description |
| :--- | :--- | :--- |
| `*:count` | `Count` | Count all rows in group |
| `Amount:sum` | `AmountSum` | Sum of Amount field |
| `Amount:avg` | `AmountAvg` | Average of Amount field |
| `Amount:sum:TotalSales` | `TotalSales` | Sum with explicit alias |

**FQL format:**
```
aggregates=FUNCTION(Field) [AS Alias]
```

| FQL Example | Alias Generated | Description |
| :--- | :--- | :--- |
| `COUNT(*)` | `Count` | Count all rows in group |
| `SUM(Amount)` | `AmountSum` | Sum of Amount field |
| `AVG(Amount)` | `AmountAvg` | Average of Amount field |
| `SUM(Amount) AS TotalSales` | `TotalSales` | Sum with explicit alias |

Auto-generated aliases follow PascalCase convention (`AmountSum`, `PriceAvg`, `Count`).
Explicitly provided aliases are preserved exactly as written.

---

## HAVING Syntax

The `having` parameter filters groups after aggregation.

**DSL:**
```
having=function:field:operator:value
having=function(field):operator:value
```

| DSL Example | Meaning |
| :--- | :--- |
| `having=count:gt:5` | Groups with count > 5 |
| `having=sum(Amount):gt:1000` | Groups with sum > 1000 |
| `having=avg(Amount):between:100,500` | Groups with avg between 100 and 500 |

**FQL:**
```
having=FUNCTION(Field) OPERATOR value
```

| FQL Example | Meaning |
| :--- | :--- |
| `having=COUNT(*) > 5` | Groups with count > 5 |
| `having=SUM(Amount) > 1000` | Groups with sum > 1000 |
| `having=AVG(Price) >= 500` | Groups with avg >= 500 |

Supported HAVING operators (DSL): `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `between`
Supported HAVING operators (FQL): `=`, `!=`, `>`, `>=`, `<`, `<=`

---

## C# Examples

### Programmatic Grouping

```csharp
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;

var options = new QueryOptions
{
    GroupBy = new List<string> { "status" },
    Aggregates = new List<AggregateModel>
    {
        new AggregateModel { Function = AggregateFunction.Count, Alias = "allCount" }
    }
};

var result = await _context.Users.ApplySelect(options).ToListAsync();
```

### With HAVING

```csharp
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;

var options = new QueryOptions
{
    GroupBy = new List<string> { "status" },
    Aggregates = new List<AggregateModel>
    {
        new AggregateModel { Function = AggregateFunction.Count, Alias = "allCount" }
    },
    Having = new HavingCondition
    {
        Function = AggregateFunction.Count,
        Operator = "gt",
        Value    = "5"
    }
};
```

---

## Common Mistakes

### ❌ Mixing aggregates in `select`

Aggregates must use the dedicated `aggregates` parameter, not `select`.

```
# WRONG (v3 style)
GET /api/users?select=status,count()&groupBy=status
```

```
# CORRECT (v4)
GET /api/users?select=status&aggregates=*:count&groupBy=status
```

### ❌ Selecting non-grouped fields

When using GROUP BY, you can only select grouped fields and aggregates. Selecting `name` when grouping by `status` is undefined behavior.

```
# WRONG
GET /api/users?select=status,name&aggregates=*:count&groupBy=status
```

```
# CORRECT — only grouped fields + aggregates
GET /api/users?select=status&aggregates=*:count&groupBy=status
```

### ❌ Using HAVING without GROUP BY

HAVING requires GROUP BY. Without it, the behavior is undefined.

---

## Performance Notes

- GROUP BY with aggregates is fully translated to SQL — no client-side evaluation.
- HAVING is translated to a SQL `HAVING` clause.
- Sort by aggregate (e.g., `count():desc`) is evaluated after grouping.
- For large aggregations, ensure the grouped columns are indexed.
