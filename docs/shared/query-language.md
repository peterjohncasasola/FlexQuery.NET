# Query Language Reference

FlexQuery.NET supports two query input formats. The parser is selected explicitly per request via `QuerySyntax` — no auto-detection.

---

## Format 1: DSL (Default)

Simple, URL-friendly colon-delimited format.

```
filter=field:operator:value
```

**Examples:**

```
filter=status:eq:active
filter=age:gte:18
filter=name:contains:alice
filter=status:in:active,pending
filter=age:between:18,65
filter=deletedAt:isnull
```

**Multiple conditions (AND by default):**

```
filter=status:eq:active,age:gte:18,name:contains:alice
```

**OR logic:**

```
filter=status:eq:active,status:eq:pending&logic=or
```

---

## Format 2: FQL (SQL-like)

Natural language SQL-like syntax. FQL supports all query parameters, not just filters.

```
filter=expression
```

**Filter examples:**

```
filter=status = "active"
filter=age >= 18
filter=name = "alice" OR name = "bob"
filter=(status = "active" OR status = "pending") AND age >= 18
filter=Orders.any(Status = "shipped")
filter=Orders.any(Status = "shipped" AND Amount > 100)
```

**FQL supported operators:**

| FQL | Meaning |
| :--- | :--- |
| `=` | eq |
| `!=` | neq |
| `>` | gt |
| `>=` | gte |
| `<` | lt |
| `<=` | lte |
| `CONTAINS` | contains |
| `STARTSWITH` | startswith |
| `ENDSWITH` | endswith |
| `IN (...)` | in |
| `LIKE` | like |
| `BETWEEN x AND y` | between |
| `IS NULL` | isnull |
| `IS NOT NULL` | isnotnull |
| `.any(...)` | any on collection |
| `.all(...)` | all on collection |
| `[...]` | bracket syntax for any |
| `COUNT` | count on collection |

**FQL sort:**
```
sort=Name ASC
sort=CreatedDate DESC
sort=Customer.Name ASC, CreatedDate DESC
```

**FQL aggregates:**
```
aggregates=SUM(Amount)
aggregates=SUM(Amount) AS TotalSales
aggregates=COUNT(*)
aggregates=AVG(Price), MIN(Date), MAX(Date)
```

**FQL having:**
```
having=COUNT(*) > 5
having=SUM(Amount) > 1000
having=AVG(Price) >= 500
```

**FQL groupBy:**
```
groupBy=Department
groupBy=Department,Category
groupBy=Customer.Region
```

---

## Sort Syntax

**DSL (colon-delimited):**
```
sort=field:direction,field:direction
```

| DSL Example | Meaning |
| :--- | :--- |
| `sort=name:asc` | Sort by name ascending |
| `sort=createdAt:desc` | Sort by createdAt descending |
| `sort=name:asc,createdAt:desc` | Multi-field sort |
| `sort=name` | Ascending (direction optional) |
| `sort=orders.count():desc` | Sort by collection count |
| `sort=orders.sum(amount):desc` | Sort by collection sum |

**FQL (SQL-inspired):**
```
sort=Name ASC
sort=Name DESC
sort=Customer.Name ASC, CreatedDate DESC
sort=name          (defaults to ASC)
```

---

## Select Syntax

```
select=field1,field2,nested.field
```

| Example | Meaning |
| :--- | :--- |
| `select=Id,Name,Email` | Top-level fields |
| `select=Id,Name,Profile.Bio` | Nested path |
| `select=*` | All fields (wildcard) |

Note: Aggregates are no longer specified in the `select` parameter. Use the dedicated `aggregates` parameter instead.

---

## Aggregates Syntax

**DSL:**
```
aggregates=Field:Function[:Alias]
```

| DSL Example | Generated Alias | Meaning |
| :--- | :--- | :--- |
| `Amount:sum` | `AmountSum` | Sum of Amount |
| `Amount:sum:TotalSales` | `TotalSales` | Sum with explicit alias |
| `Price:avg` | `PriceAvg` | Average of Price |
| `*:count` | `Count` | Count all rows |
| `Date:min,Date:max` | `DateMin`, `DateMax` | Min and max |

**FQL:**
```
aggregates=FUNCTION(Field) [AS Alias]
```

| FQL Example | Generated Alias | Meaning |
| :--- | :--- | :--- |
| `SUM(Amount)` | `AmountSum` | Sum of Amount |
| `SUM(Amount) AS TotalSales` | `TotalSales` | Sum with explicit alias |
| `COUNT(*)` | `Count` | Count all rows |
| `AVG(Price)` | `PriceAvg` | Average of Price |
| `MIN(Date), MAX(Date)` | `DateMin`, `DateMax` | Min and max |

Both DSL and FQL support: `SUM`, `COUNT`, `AVG`, `MIN`, `MAX`.

Function names are case-insensitive. Auto-generated aliases follow PascalCase convention.

---

## GroupBy Syntax

```
groupBy=field1,field2
```

| Example | Meaning |
| :--- | :--- |
| `groupBy=Department` | Single field |
| `groupBy=Department,Category` | Multiple fields |
| `groupBy=Customer.Region` | Nested property |

---

## Having Syntax

**DSL:**
```
having=function(field):operator:value
```

| DSL Example | Meaning |
| :--- | :--- |
| `having=count:gt:5` | Groups with count > 5 |
| `having=sum(total):gt:1000` | Groups with sum > 1000 |
| `having=avg(amount):between:100,500` | Groups with avg in range |

**FQL:**
```
having=FUNCTION(Field) OPERATOR value
```

| FQL Example | Meaning |
| :--- | :--- |
| `having=COUNT(*) > 5` | Groups with count > 5 |
| `having=SUM(Amount) > 1000` | Groups with sum > 1000 |
| `having=AVG(Price) >= 500` | Groups with avg >= 500 |

Supported HAVING operators: `=`, `!=`, `>`, `>=`, `<`, `<=` (FQL); `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `between` (DSL).

---

## Include Syntax

```
include=Navigation
include=Navigation(filter)
include=Navigation1,Navigation2
include=Navigation(filter).NestedNavigation
```

| Example | Meaning |
| :--- | :--- |
| `include=Orders` | Include all orders |
| `include=Orders(status:eq:shipped)` | Include only shipped orders |
| `include=Orders,Profile` | Include multiple navigations |

---

## Paging Parameters

| Parameter | Default | Description |
| :--- | :--- | :--- |
| `page` | `1` | Page number (1-indexed) |
| `pageSize` | `20` | Items per page |
| `includeCount` | `true` | Include `totalCount` in response |

---

## Other Parameters

| Parameter | Values | Description |
| :--- | :--- | :--- |
| `mode` | `nested`, `flat`, `flat-mixed` | Projection output shape |
| `distinct` | `true`, `false` | Apply DISTINCT |
| `logic` | `and`, `or` | Top-level filter logic |
