# Grouping & Aggregates

FlexQuery.NET supports server-side GROUP BY with aggregate projections (sum, count, avg) and HAVING conditions — all driven by query parameters.

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

```
GET /api/users?select=status,count()&groupBy=status
```

**Response:**
```json
{
  "data": [
    { "status": "active",   "COUNT_All": 42 },
    { "status": "inactive", "COUNT_All": 6  },
    { "status": "pending",  "COUNT_All": 12 }
  ],
  "totalCount": 3
}
```

### Sum of Orders by User

```
GET /api/orders?select=userId,sum(amount)&groupBy=userId
```

**Response:**
```json
{
  "data": [
    { "userId": 1, "SUM_Amount": 1250.00 },
    { "userId": 2, "SUM_Amount": 780.50  }
  ]
}
```

### Average with HAVING

```
GET /api/orders?select=customerId,avg(amount)&groupBy=customerId&having=avg(amount):gt:500
```

Only returns groups where the average order amount exceeds 500.

**Response:**
```json
{
  "data": [
    { "customerId": 1, "AVG_Amount": 625.00 },
    { "customerId": 5, "AVG_Amount": 812.50 }
  ]
}
```

### Sort by Aggregate

```
GET /api/users?select=status,count()&groupBy=status&sort=count():desc
```

---

## Aggregate Syntax

In the `select` parameter, use function call syntax:

| Syntax | Alias Generated | Description |
| :--- | :--- | :--- |
| `count()` | `COUNT_All` | Count all rows in group |
| `sum(amount)` | `SUM_Amount` | Sum of `amount` field |
| `avg(amount)` | `AVG_Amount` | Average of `amount` field |

Alias format: `{FUNCTION}_{Field}` (uppercase function, PascalCase field).

---

## HAVING Syntax

The `having` parameter filters groups after aggregation.

```
having=count():gt:5
having=sum(amount):gte:1000
having=avg(amount):between:100,500
```

Supported operators in HAVING: `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `between`

---

## C# Examples

### Programmatic Grouping

```csharp
var options = new QueryOptions
{
    GroupBy = new List<string> { "status" },
    Aggregates = new List<AggregateModel>
    {
        new AggregateModel { Function = "count", Alias = "COUNT_All" }
    }
};

var result = await _context.Users.ApplySelect(options).ToListAsync();
```

### With HAVING

```csharp
var options = new QueryOptions
{
    GroupBy = new List<string> { "status" },
    Aggregates = new List<AggregateModel>
    {
        new AggregateModel { Function = "count", Alias = "COUNT_All" }
    },
    Having = new HavingCondition
    {
        Function = "count",
        Operator = "gt",
        Value    = "5"
    }
};
```

---

## Common Mistakes

### ❌ Selecting non-grouped fields

When using GROUP BY, you can only select grouped fields and aggregates. Selecting `name` when grouping by `status` is undefined behavior.

```
# WRONG
GET /api/users?select=status,name,count()&groupBy=status
```

```
# CORRECT — only grouped fields + aggregates
GET /api/users?select=status,count()&groupBy=status
```

### ❌ Using HAVING without GROUP BY

HAVING requires GROUP BY. Without it, the behavior is undefined.

---

## Performance Notes

- GROUP BY with aggregates is fully translated to SQL — no client-side evaluation.
- HAVING is translated to a SQL `HAVING` clause.
- Sort by aggregate (e.g., `count():desc`) is evaluated after grouping.
- For large aggregations, ensure the grouped columns are indexed.
