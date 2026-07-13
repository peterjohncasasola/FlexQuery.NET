# FlexQuery.NET.Parsers.Fql

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.Fql.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.Fql)

FQL (FlexQuery Query Language) is a SQL-inspired query language for FlexQuery.NET.

It provides an expressive and familiar syntax for filtering, sorting, grouping, aggregation, and projection while integrating seamlessly with the FlexQuery.NET query pipeline.

---

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.Fql
```

## Registration

```csharp
Fql.Register();
```

---

# When to Use FQL

Use FQL when your API consumers prefer SQL-like query expressions instead of the native FlexQuery DSL.

FQL is ideal for:

- SQL-inspired filtering
- Complex logical expressions
- Nested property filtering
- Collection navigation (`Any`, `All`, `Count`)
- Aggregation and grouping
- Rich REST API querying

---

# HTTP Request Examples

## Basic Filtering

```http
GET /api/users?filter=Status = 'Active'
```

---

## Multiple Conditions

```http
GET /api/users?filter=Status = 'Active' AND Age >= 18
```

```http
GET /api/products?filter=Category = 'Books' OR Category = 'Electronics'
```

---

## Nested Property Filtering

```http
GET /api/orders?filter=Customer.Country = 'Japan'
```

---

## String Operations

```http
GET /api/users?filter=Name CONTAINS 'john'
```

```http
GET /api/users?filter=Email STARTSWITH 'admin'
```

```http
GET /api/users?filter=Email ENDSWITH '.com'
```

```http
GET /api/users?filter=Name LIKE '%john%'
```

---

## Null Checks

```http
GET /api/users?filter=DeletedAt IS NULL
```

```http
GET /api/users?filter=DeletedAt IS NOT NULL
```

---

## Collection Operators

```http
GET /api/users?filter=Status IN ('Active','Pending')
```

```http
GET /api/products?filter=Price BETWEEN 1000 AND 5000
```

---

## Collection Navigation

### Any

```http
GET /api/customers?filter=Orders.Any(Status = 'Completed')
```

```http
GET /api/customers?filter=Orders.Any(Status = 'Completed' AND Total > 1000)
```

### Nested Collections

```http
GET /api/customers?filter=Orders.Any(
    Status = 'Completed'
    AND OrderItems.Any(Quantity > 5)
)
```

### Count

```http
GET /api/customers?filter=Orders.Count > 5
```

---

# Complete Example

Retrieve active orders from Japanese customers, include related entities, sort the results, calculate aggregates, and return paged results.

```http
GET /api/orders?
filter=((Status = 'Open' OR Status = 'Pending')
AND Amount > 1000
AND Customer.Country = 'Japan'
AND OrderItems.Any(Product.Price > 500))
&select=Id,OrderNo,Amount,Customer.Name
&include=Customer,OrderItems
&sort=CreatedAt DESC,Customer.Name ASC
&groupBy=Customer.Country
&aggregate=SUM(Amount) AS TotalSales,COUNT(*) AS TotalOrders
&having=SUM(Amount) > 10000
&distinct=true
&page=1
&pageSize=20
&includeCount=true
```

---

# Supported Query Parameters

| Parameter | Description |
|------------|-------------|
| `filter` | Filter records |
| `select` | Select specific fields |
| `sort` | Sort one or more fields |
| `include` | Include related entities |
| `groupBy` | Group results |
| `aggregates` | Aggregate functions |
| `having` | HAVING clause |
| `page` | Page number |
| `pageSize` | Number of records per page |
| `distinct` | Return distinct records |
| `includeCount` | Include the total record count |

---

# Supported Operators

## Comparison

```sql
=
!=
>
>=
<
<=
```

## Logical

```sql
AND
OR
```

## String

```sql
CONTAINS
STARTSWITH
ENDSWITH
LIKE
```

## Null

```sql
IS NULL
IS NOT NULL
```

## Collections

```sql
IN
NOT IN
BETWEEN
ANY
ALL
COUNT
```

---

# Features

- SQL-inspired query language
- Strict grammar validation
- Nested property paths
- Collection navigation
- Aggregate functions
- HAVING support
- Projection
- Sorting
- Paging
- Compatible with the FlexQuery.NET execution pipeline

---

# Related Packages

- **FlexQuery.NET** – Core query engine
- **FlexQuery.NET.Parsers.Dsl** – Native FlexQuery DSL
- **FlexQuery.NET.Parsers.MiniOData** – OData-compatible query syntax

---

# Documentation

- https://flexquery.vercel.app