# Query Language Reference

FlexQuery.NET supports four query input formats. The parser **auto-detects** the format — you do not need to specify which one you are using.

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

## Format 2: JQL (SQL-like)

Natural language SQL-like syntax using the `query` parameter.

```
query=expression
```

**Examples:**

```
query=status = "active"
query=age >= 18
query=name = "alice" OR name = "bob"
query=(status = "active" OR status = "pending") AND age >= 18
query=Orders.any(Status = "shipped")
query=Orders.any(Status = "shipped" AND Amount > 100)
```

**Supported operators:**

| JQL | Meaning |
| :--- | :--- |
| `=` | eq |
| `!=` | neq |
| `>` | gt |
| `>=` | gte |
| `<` | lt |
| `<=` | lte |
| `contains(f, v)` | contains |
| `startsWith(f, v)` | startswith |
| `.any(...)` | any on collection |
| `.all(...)` | all on collection |

---

## Format 3: JSON

Structured nested filter tree.

```
filter={"logic":"and","filters":[...]}
```

**Examples:**

Simple:
```json
{"logic":"and","filters":[
  {"field":"status","operator":"eq","value":"active"},
  {"field":"age","operator":"gte","value":"18"}
]}
```

Nested OR:
```json
{
  "logic": "and",
  "filters": [
    { "field": "status", "operator": "eq", "value": "active" },
    {
      "logic": "or",
      "filters": [
        { "field": "name", "operator": "contains", "value": "alice" },
        { "field": "name", "operator": "contains", "value": "bob" }
      ]
    }
  ]
}
```

---

## Format 4: Indexed (Form-based)

Array-indexed parameters for form submissions.

```
filter[0].field=status
filter[0].operator=eq
filter[0].value=active
filter[1].field=age
filter[1].operator=gte
filter[1].value=18
logic=and
```

---

## Sort Syntax

```
sort=field:direction,field:direction
```

| Example | Meaning |
| :--- | :--- |
| `sort=name:asc` | Sort by name ascending |
| `sort=createdAt:desc` | Sort by createdAt descending |
| `sort=name:asc,createdAt:desc` | Multi-field sort |
| `sort=name` | Ascending (direction optional) |
| `sort=orders.count():desc` | Sort by collection count |
| `sort=orders.sum(amount):desc` | Sort by collection sum |

---

## Select Syntax

```
select=field1,field2,nested.field
```

| Example | Meaning |
| :--- | :--- |
| `select=id,name,email` | Top-level fields |
| `select=id,name,profile.bio` | Nested path |
| `select=*` | All fields (wildcard) |
| `select=status,count()` | Field + aggregate |
| `select=status,sum(amount)` | Field + sum aggregate |

---

## GroupBy Syntax

```
groupBy=field1,field2
```

---

## Having Syntax

```
having=function(field):operator:value
```

| Example | Meaning |
| :--- | :--- |
| `having=count():gt:5` | Groups with count > 5 |
| `having=sum(amount):gte:1000` | Groups with sum >= 1000 |
| `having=avg(amount):between:100,500` | Groups with avg in range |

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
| `logic` | `and`, `or` | Top-level filter logic (Indexed format) |
