> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Strongly-Typed Query Usage

FlexQuery.NET is uniquely designed to support **both** dynamic string-based queries and first-class strongly-typed object models. While many libraries focus solely on parsing URL strings, FlexQuery treats its internal object model as a primary API. 

This dual-mode approach allows you to bridge the gap between flexible client-side filtering and robust, server-side business logic.

> [!TIP]
> **Mental Model**: Think of `QueryOptions` as a structured representation of a query, similar to a serialized expression tree. It allows you to describe *what* data you want without being tied to a specific LINQ provider or string syntax.

## Basic Example

Instead of relying on string parsing, you can build your query logic directly using the `QueryOptions` and `FilterCondition` classes. Using `FilterOperators` constants ensures your code is clean and resistant to typos.

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

// Apply directly to any IQueryable
var results = await _context.Products
    .ApplyQueryOptions(options)
    .ToListAsync();
```

## Handling Complex Logic (Nested Filters)

For scenarios requiring complex `AND` and `OR` combinations, you can nest `FilterGroup` objects. This is where strongly-typed queries significantly outperform string manipulation.

```csharp
// Logic: Status == 'Active' AND (Category == 'Electronics' OR Category == 'Gadgets')
var options = new QueryOptions
{
    Filter = new FilterGroup
    {
        Logic = LogicOperator.And,
        Filters = new List<FilterCondition>
        {
            new FilterCondition { Field = "Status", Value = "Active" }
        },
        Groups = new List<FilterGroup>
        {
            new FilterGroup
            {
                Logic = LogicOperator.Or,
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "Category", Value = "Electronics" },
                    new FilterCondition { Field = "Category", Value = "Gadgets" }
                }
            }
        }
    }
};
```

## Why Use Strongly-Typed Queries?

*   **Structured Queries**: Work with a formal hierarchy of objects instead of loosely structured strings. This makes your query logic part of your code's architecture.
*   **Dynamic Construction**: Easily build queries piece-by-piece using standard C# collection initializers and `if` statements. No more fragile string concatenation.
*   **Maintainability**: Strongly-typed models are easier to refactor, test, and pass between different layers of your application.
*   **Zero Parsing Reliance**: By bypassing the string parser, you eliminate a layer of overhead and potential security concerns related to raw input processing for internal tasks.

## Dynamic Example: Conditional Search

A common production use case is a search service that applies filters based on which parameters are provided by the caller.

```csharp
public async Task<List<Product>> SearchProducts(string? category, decimal? minPrice)
{
    var options = new QueryOptions();
    var filter = new FilterGroup { Logic = LogicOperator.And };

    if (!string.IsNullOrEmpty(category))
    {
        filter.Filters.Add(new FilterCondition 
        { 
            Field = "Category", 
            Operator = FilterOperators.Equal,
            Value = category 
        });
    }

    if (minPrice.HasValue)
    {
        filter.Filters.Add(new FilterCondition 
        { 
            Field = "Price", 
            Operator = FilterOperators.GreaterThanOrEq, 
            Value = minPrice.Value.ToString() 
        });
    }

    options.Filter = filter;

    return await _context.Products
        .ApplyQueryOptions(options)
        .ToListAsync();
}
```

## Comparison: Strings vs. Objects

| Feature | String-Based (`?filter=...`) | Strongly-Typed (`QueryOptions`) |
| :--- | :--- | :--- |
| **Primary Source** | External (Frontend, Mobile, API) | Internal (Services, Repositories) |
| **Flexibility** | High: Let clients decide filters | Precise: Server defines exact query logic |
| **Readability** | Excellent for simple URI-based filters | Excellent for complex, nested C# logic |
| **Workflow** | `Parse` → `Validate` → `Apply` | `Instantiate` → `Apply` |

## Recommendation

*   **External Input (APIs)**: Use **String-Based** queries. This is the library's bread and butter, providing a flexible way for consumers to shape their own results.
*   **Internal Logic (Services)**: Use **Strongly-Typed** queries. This provides the most control and maintainability when building queries inside your business layer.
*   **Hybrid Approach**: You can parse a client's query string into a `QueryOptions` object and then *modify* that object in C# (e.g., adding mandatory security filters) before execution. This gives you the best of both worlds.

