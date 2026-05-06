
# Strongly-Typed Query Usage

FlexQuery.NET supports two complementary query models:

- **String-based queries** (`?filter=...`) for external/API-driven scenarios
- **Strongly-typed query objects** (`QueryOptions`, `FilterGroup`, `FilterCondition`) for advanced server-side query composition

Unlike many query libraries that treat object models as internal implementation details, FlexQuery.NET intentionally exposes its structured query model as an advanced public API.

This enables:

- strongly-typed query composition
- dynamic runtime query generation
- nested logical groups
- server-side query augmentation
- hybrid API + programmatic filtering

---

# Query API Levels

| API Level | Recommended For | Entry Point |
|---|---|---|
| High-Level API | Controllers, APIs, frontend-driven filtering | `FlexQueryParameters + FlexQuery()` |
| Advanced API | Server-side query composition, services, dynamic builders | `QueryOptions + ApplyQueryOptions()` |

---

# High-Level API (Recommended)

```csharp
var result = _context.Products.FlexQuery(parameters);
```

Use this for:
- APIs
- frontend grids
- external query input
- URL-driven filtering

---

# Advanced API (Strongly-Typed)

```csharp
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;

var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Logic = LogicOperator.And,
        Filters = new List<FilterCondition>
        {
            new FilterCondition
            {
                Field = "Status",
                Operator = FilterOperators.Equal,
                Value = "Active"
            },
            new FilterCondition
            {
                Field = "Price",
                Operator = FilterOperators.GreaterThan,
                Value = "100"
            }
        }
    }
};

var results = await _context.Products
    .ApplyQueryOptions(options)
    .ToListAsync();
```

---

# Nested Logical Groups

```csharp
var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Logic = LogicOperator.And,

        Filters = new List<FilterCondition>
        {
            new FilterCondition
            {
                Field = "Status",
                Operator = FilterOperators.Equal,
                Value = "Active"
            }
        },

        Groups = new List<FilterGroup>
        {
            new FilterGroup
            {
                Logic = LogicOperator.Or,

                Filters = new List<FilterCondition>
                {
                    new FilterCondition
                    {
                        Field = "Category",
                        Operator = FilterOperators.Equal,
                        Value = "Electronics"
                    },

                    new FilterCondition
                    {
                        Field = "Category",
                        Operator = FilterOperators.Equal,
                        Value = "Gadgets"
                    }
                }
            }
        }
    }
};
```

---

# Fluent Builder Integration

```csharp
var filtered = _context.Users
    .Filter<User>(f => f
        .Group(g => g
            .Field(x => x.Name).Contains("John")
            .And(x => x.Age).GreaterThan(18))
        .OrGroup(g => g
            .Field(x => x.Status).Eq("Active")
            .And(x => x.Role).Eq("Admin")))
    .ToList();
```

---

# Comparison: String vs Strongly-Typed

| Feature | String-Based Queries | Strongly-Typed Queries |
|---|---|---|
| Main Use Case | External API input | Internal server logic |
| Type Safety | Low | High |
| Parsing Required | Yes | No |
| Nested Logic | Harder | Natural |
| Refactor Safety | Lower | Higher |

---

# Recommendations

## Use String-Based Queries When

- building APIs
- accepting user-defined filters
- supporting frontend grids

Preferred API:

```csharp
query.FlexQuery(parameters);
```

---

## Use Strongly-Typed Queries When

- building internal services
- composing filters dynamically
- implementing business rules
- creating reusable query templates

Preferred API:

```csharp
query.ApplyQueryOptions(options);
```

---

# Final Notes

`QueryOptions` is intentionally exposed as an advanced low-level API.

While most applications should prefer:

```csharp
FlexQueryParameters + FlexQuery()
```

the strongly-typed query model remains a first-class feature for advanced programmatic query composition.
