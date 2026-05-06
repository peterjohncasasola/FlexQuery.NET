> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Grouping & Aggregates

FlexQuery.NET allows you to perform server-side grouping and aggregation, enabling powerful reporting capabilities directly through query parameters.

## Grouping

Use the `group` parameter to specify one or more fields to group by.

**Example:**
`?group=category,status`

## Aggregate Functions

You can use aggregate functions within the `select` and `having` parameters to compute values over the grouped data.

| Function | Description | Example |
|---|---|---|
| `sum(field)` | Computes the sum of a numeric field | `sum(total)` |
| `count(field)` | Counts the number of non-null values | `count(id)` |
| `avg(field)` | Computes the average of a numeric field | `avg(price)` |
| `max(field)` | Finds the maximum value | `max(createdAt)` |
| `min(field)` | Finds the minimum value | `min(price)` |

## Post-Aggregation Filtering (Having)

The `having` parameter allows you to filter the results **after** aggregation has been performed. This is equivalent to the SQL `HAVING` clause.

**Example Request:**
```http
GET /api/orders
  ?group=category,status
  &select=category,sum(total) as totalVolume,count(id) as orderCount
  &having=sum(total):gt:10000
```

## Pre-Aggregation vs. Post-Aggregation

- **Filtering (`filter` / `query`)**: These are applied **before** grouping (SQL `WHERE`). They limit which individual records are included in the aggregation.
- **Having (`having`)**: This is applied **after** grouping (SQL `HAVING`). It limits which groups are returned based on their computed aggregate values.

## LINQ Translation

FlexQuery.NET translates these parameters into efficient LINQ `GroupBy` and `Select` expressions.

**Conceptual C# Translation:**
```csharp
query.GroupBy(x => new { x.Category, x.Status })
     .Select(g => new {
         category = g.Key.Category,
         totalVolume = g.Sum(x => x.Total),
         orderCount = g.Count()
     })
     .Where(x => x.totalVolume > 10000);
```

By performing these operations at the database level, you minimize data transfer and leverage the power of the database engine for analytical queries.

