> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Filtering

FlexQuery.NET provides a powerful dynamic filtering engine that translates client-side requests into native .NET Expression Trees. These expressions are passed directly to your database provider (like EF Core), ensuring that filtering happens at the database level, not in memory.

FlexQuery supports four primary syntaxes:
- **DSL (Domain Specific Language)**: Optimized for simple, structured query strings.
- **JQL (Jira Query Language)**: An advanced, expression-based syntax for complex logic and scoped filtering.
- **JSON**: Best for programmatic queries or frontend libraries.
- **Generic (Indexed)**: Compatible with traditional form-data or standard grid libraries.

## Basic Usage

You can apply filters using the `filter` parameter for DSL or the `query` parameter for JQL.

**DSL Example:**
```http
GET /api/products?filter=price:gt:100&sort=createdDate:desc
```

**JQL Example:**
```http
GET /api/products?query=price > 100 AND status = 'Active'
```

**JSON Example:**
```http
GET /api/products?filter={"logic":"and","filters":[{"field":"Price","operator":"gt","value":100}]}
```

**Generic Example:**
```http
GET /api/products?filter[0].field=Price&filter[0].operator=gt&filter[0].value=100
```

---

## DSL Syntax (Recommended)

The DSL uses a structured `field:operator:value` format. It is designed to be easy to parse and safe to expose in public APIs.

### Format
```text
field:operator:value
```

### Examples
- **Equality**: `?filter=status:eq:Active`
- **Comparison**: `?filter=price:gt:100`
- **String Search**: `?filter=name:contains:john`

### Multiple Conditions
- **AND (`,`)**: `?filter=status:eq:Active,price:gt:100`
- **OR (`|`)**: `?filter=status:eq:Active|status:eq:Pending`

### Nested Logic
Grouping is supported using parentheses to control evaluation order:

**Example:**
`?filter=((city:eq:London|city:eq:Berlin)&(age:gt:25))`

- Logic symbols: `&` (AND), `|` (OR), `,` (AND).
- Parentheses can be nested to any depth.

### Supported Operators
| Operator | Description |
| :--- | :--- |
| `eq`, `neq` | Equal, Not Equal |
| `gt`, `lt` | Greater Than, Less Than |
| `gte`, `lte` | Greater/Less Than or Equal |
| `contains` | Substring search |
| `startswith` | Prefix search |
| `endswith` | Suffix search |

*For a full list, see the [Operators Reference](/shared/operators).*

---

## Collection Predicates (Any / All)

Filter parent entities based on their related collections using `any` or `all`.

- **any**: Returns parents where at least one element in the collection matches the sub-filter.
- **all**: Returns parents where every element in the collection matches the sub-filter.

**Examples:**
- **At least one large order**: `?filter=orders:any:total:gt:500`
- **All items shipped**: `?filter=orders:all:status:eq:Shipped`

---

## JQL (Advanced Filtering)

JQL provides a more expressive, SQL-like syntax for complex requirements. It is best used for internal systems or advanced search interfaces.

**Example:**
```http
GET /api/products?query=status = 'Active' AND price > 100
```

### Scoped Filtering
By default, multiple conditions on a collection are interpreted as separate `Any()` checks. **Scoped Filtering** ensures that multiple conditions apply to the **same element** within the collection.

**Example:**
```http
GET /api/orders?query=orders.any(status = 'Cancelled' AND total > 500)
```

**Equivalent LINQ:**
```csharp
x => x.Orders.Any(o => o.Status == "Cancelled" && o.Total > 500)
```

### Nested Scoped Filtering
Scoped filters can be nested recursively to navigate deep object graphs:
```http
GET /api/customers?query=orders.any(status = 'Cancelled' AND orderItems.any(productId = 101))
```

---

## DSL vs JQL Comparison

| Feature | DSL | JQL |
| :--- | :--- | :--- |
| **Syntax** | Structured (`field:op:val`) | Expression-based |
| **Complexity** | Low | Medium / High |
| **Use Case** | Public APIs, Smart Grids | Advanced Search, Power Users |
| **Readability** | High for simple queries | High for logical expressions |

**Recommendation**: Use **DSL** for 90% of your API needs. Use **JQL** when you need scoped collection filtering or highly complex logical grouping.

---

## Technical Details

### Priority Rules
If both `filter` and `query` are provided in the same request, the **JQL (`query`) takes precedence** and the DSL filter is ignored.

### Case Sensitivity
- **Field Names**: Case-insensitive (mapped to property names via reflection).
- **Values**: Depends on your database provider's collation (EF Core usually follows the DB default).

### String Values
- **JQL**: Wrap strings in single quotes: `?query=name = 'John'`.
- **DSL**: No quotes required: `?filter=name:eq:John`.

### Invalid Fields
By default, FlexQuery **ignores** fields that do not exist on the target model. To change this behavior for stricter security:
```csharp
options.StrictFieldValidation = true; // Throws QueryValidationException on invalid fields
```

---

## Real-World Example

**Request:**
```http
GET /api/orders?filter=status:eq:Active&sort=createdDate:desc&page=1&pageSize=10
```

**What Happens:**
1. **Parsing**: The `QueryOptionsParser` reads the query string and identifies the "Active" status filter.
2. **Translation**: FlexQuery builds an expression: `x => x.Status == "Active"`.
3. **LINQ Chain**: `.Where(x => x.Status == "Active").OrderByDescending(x => x.CreatedDate).Skip(0).Take(10)` is applied to the `IQueryable`.
4. **Execution**: The DB provider translates this into a single SQL query and returns the results.

---

## Common Mistakes

- **Invalid Field**: `?filter=unknown:eq:123` (Ignored by default).
- **Wrong Operator**: `?filter=price:greater:100` (Should be `gt`).
- **Mixing Syntax**: `?filter=status = 'Active'` (DSL doesn't use `=`, use `eq` or move to `?query=`).

---

## JSON Filtering

For scenarios where queries are generated programmatically (e.g., from a frontend query builder), you can provide the entire filter structure as a JSON object.

**Format:**
```http
GET /api/products?filter={"logic":"and","filters":[{"field":"Price","operator":"gt","value":100}]}
```

**JSON Structure (Nested Logic):**
You can nest filters by adding a group object (containing its own `logic` and `filters`) inside the top-level `filters` array.

```json
{
  "logic": "and",
  "filters": [
    { "field": "Price", "operator": "gt", "value": 100 },
    {
      "logic": "or",
      "filters": [
        { "field": "Category", "operator": "eq", "value": "Electronics" },
        { "field": "Category", "operator": "eq", "value": "Books" }
      ]
    }
  ]
}
```


---

## Generic (Indexed) Filtering

This format is compatible with many legacy grid libraries and form-data structures. It uses indexed keys for conditions.

**Format:**
```http
GET /api/products?filter[0].field=Name&filter[0].operator=contains&filter[0].value=john
```

- **Multiple Conditions**: Increment the index (e.g., `filter[1].field=Price`).
- **Global Logic**: Use the `logic` parameter (e.g., `&logic=or`).

---

## Summary
- **DSL (`filter`)**: Recommended for most REST APIs.
- **JQL (`query`)**: For advanced scoped queries and complex logic.
- **JSON**: Ideal for programmatic integration.
- **Generic**: Best for compatibility with indexed-field libraries.


