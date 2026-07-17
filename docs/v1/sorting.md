> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Sorting

FlexQuery.NET offers a flexible sorting system that supports multi-field ordering, nested property paths, and aggregate-based sorting for collections.

## Basic Sorting

Sort by a single field using the `sort` parameter.

- **Ascending**: `?sort=name:asc` (or simply `?sort=name`)
- **Descending**: `?sort=createdAt:desc`

## Multi-field Sorting

Chain multiple fields by separating them with commas. The library applies them in the order provided.

`?sort=status:asc,createdAt:desc,id:asc`

## Nested Sorting

Sort by properties of related entities using dot notation.

`?sort=customer.name:asc`

## Aggregate Sorting

You can sort parent entities based on aggregated values of their child collections.

| Function | Description |
|---|---|
| `sum(field)` | Sort by the sum of a numeric field |
| `count()` | Sort by the number of items in the collection |
| `avg(field)` | Sort by the average value of a numeric field |
| `max(field)` | Sort by the maximum value |
| `min(field)` | Sort by the minimum value |

**Examples:**
- `?sort=orders.sum(total):desc` (Customers with highest total order volume first)
- `?sort=orders.count():desc` (Customers with the most orders first)

> [!NOTE]
> Direct collection sorting (e.g., `?sort=orders.total`) is **not** supported; you must use an aggregate function to reduce the collection to a single value for comparison.

