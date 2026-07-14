# Operators Reference

All filter operators supported by FlexQuery.NET, with examples, SQL translations, and supported types.

---

## Comparison Operators

| Operator | Aliases | Description | Supported Types |
| :--- | :--- | :--- | :--- |
| `eq` | `equal`, `equals`, `==`, `=` | Equals | All |
| `neq` | `ne`, `notequal`, `!=` | Not equals | All |
| `gt` | `greaterthan`, `>` | Greater than | Numeric, Date |
| `gte` | `ge`, `greaterthanorequal`, `>=` | Greater than or equal | Numeric, Date |
| `lt` | `lessthan`, `<` | Less than | Numeric, Date |
| `lte` | `le`, `lessthanorequal`, `<=` | Less than or equal | Numeric, Date |

### Examples

```
GET /api/users?filter=age:eq:30
GET /api/users?filter=age:gte:18
GET /api/users?filter=createdAt:gt:2024-01-01
GET /api/users?filter=price:lte:99.99
```

**SQL:**
```sql
WHERE Age = 30
WHERE Age >= 18
WHERE CreatedAt > '2024-01-01'
WHERE Price <= 99.99
```

---

## String Operators

| Operator | Aliases | Description |
| :--- | :--- | :--- |
| `contains` | `cn` | Substring match (LIKE '%val%') |
| `startswith` | `starts`, `sw` | Prefix match (LIKE 'val%') |
| `endswith` | `ends`, `ew` | Suffix match (LIKE '%val') |
| `like` | — | Raw SQL LIKE pattern |

### Examples

```
GET /api/users?filter=name:contains:alice
GET /api/users?filter=email:startswith:admin
GET /api/users?filter=email:endswith:.com
GET /api/users?filter=name:like:%ali%
```

**SQL:**
```sql
WHERE Name LIKE '%alice%'
WHERE Email LIKE 'admin%'
WHERE Email LIKE '%.com'
WHERE Name LIKE '%ali%'
```

> [!TIP]
> `startswith` is index-friendly. Prefer it over `contains` when searching large tables.

---

## Null Check Operators

| Operator | Aliases | Description |
| :--- | :--- | :--- |
| `isnull` | `null` | Field is NULL |
| `isnotnull` | `notnull`, `isnotempty` | Field is NOT NULL |

### Examples

```
GET /api/users?filter=deletedAt:isnull
GET /api/users?filter=profilePicture:isnotnull
```

**SQL:**
```sql
WHERE DeletedAt IS NULL
WHERE ProfilePicture IS NOT NULL
```

---

## List Operators

| Operator | Aliases | Description |
| :--- | :--- | :--- |
| `in` | — | Value in comma-separated list |
| `notin` | `not in` | Value not in list |

### Examples

```
GET /api/users?filter=status:in:active,pending,trial
GET /api/users?filter=status:notin:deleted,banned
```

**SQL:**
```sql
WHERE Status IN ('active', 'pending', 'trial')
WHERE Status NOT IN ('deleted', 'banned')
```

---

## Range Operator

| Operator | Description |
| :--- | :--- |
| `between` | Inclusive range (min and max) |

### Example

```
GET /api/users?filter=age:between:18,65
GET /api/orders?filter=amount:between:100,500
```

**SQL:**
```sql
WHERE Age BETWEEN 18 AND 65
WHERE Amount BETWEEN 100.00 AND 500.00
```

---

## Collection Operators

These operators work on navigation collection properties.

| Operator | Description | SQL Translation |
| :--- | :--- | :--- |
| `any` | At least one element matches | `EXISTS` subquery |
| `all` | All elements match | `NOT EXISTS ... NOT` subquery |
| `count` | Collection count comparison | Subquery with COUNT |

### any

```
GET /api/users?filter=orders:any:status:eq:shipped
```

**SQL:**
```sql
WHERE EXISTS (
  SELECT 1 FROM Orders o WHERE o.UserId = u.Id AND o.Status = 'shipped'
)
```

### all

```
GET /api/users?filter=orders:all:status:eq:confirmed
```

**SQL:**
```sql
WHERE NOT EXISTS (
  SELECT 1 FROM Orders o WHERE o.UserId = u.Id AND o.Status != 'confirmed'
)
```

### count

```
GET /api/users?filter=orders:count:gt:5
```

**SQL:**
```sql
WHERE (SELECT COUNT(*) FROM Orders o WHERE o.UserId = u.Id) > 5
```

### Nested any (JQL)

```
GET /api/users?query=Orders.any(Status = "shipped" AND Amount > 100)
```

---

## JQL Operator Syntax

JQL uses natural language symbols:

| JQL Syntax | Operator |
| :--- | :--- |
| `=` | eq |
| `!=` | neq |
| `>` | gt |
| `>=` | gte |
| `<` | lt |
| `<=` | lte |
| `contains(field, "val")` | contains |
| `startsWith(field, "val")` | startswith |
| `.any(...)` | any (collection) |

**JQL examples:**

```
GET /api/users?query=status = "active" AND age >= 18
GET /api/users?query=(name = "alice" OR name = "bob") AND status = "active"
GET /api/users?query=Orders.any(Status = "shipped")
```

---

## Operator Normalization

All operators are normalized on parse. These are all equivalent:

```
filter=status:eq:active
filter=status:equal:active
filter=status:equals:active
filter=status:==:active
```

The normalizer maps them all to `eq` before building the expression tree.

---

## All Operators at a Glance

```csharp
public static class FilterOperators
{
    public const string Equal           = "eq";
    public const string NotEqual        = "neq";
    public const string GreaterThan     = "gt";
    public const string GreaterThanOrEq = "gte";
    public const string LessThan        = "lt";
    public const string LessThanOrEq    = "lte";
    public const string Contains        = "contains";
    public const string StartsWith      = "startswith";
    public const string EndsWith        = "endswith";
    public const string IsNull          = "isnull";
    public const string IsNotNull       = "isnotnull";
    public const string In              = "in";
    public const string NotIn           = "notin";
    public const string Between         = "between";
    public const string Like            = "like";
    public const string Any             = "any";
    public const string All             = "all";
    public const string Count           = "count";
}
```

Check if an operator is supported at runtime:

```csharp
FilterOperators.IsSupported("gte"); // true
FilterOperators.IsSupported("xyz"); // false
```
