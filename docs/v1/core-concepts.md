> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Core Concepts

Understanding the core architecture of FlexQuery.NET is essential for building scalable and secure dynamic APIs.

## QueryOptions

The `QueryOptions` object is the "Source of Truth" for any dynamic request. It contains:
- **Filter Groups**: Logical trees of conditions.
- **Sorts**: Multi-field ordering instructions.
- **Projection**: Select and Include trees.
- **Security State**: Whitelists, blacklists, and depth constraints.

## FilterGroup & FilterCondition

Filtering is represented as a recursive tree structure:
- **FilterCondition**: A leaf node (e.g., `Status eq "Active"`).
- **FilterGroup**: A container that groups conditions using a logical operator (`AND`, `OR`).

This structure allows FlexQuery.NET to represent complex logical expressions like `(A OR B) AND C` and translate them directly into C# Expression Trees.

## Dual-Pipeline Architecture (Critical)

FlexQuery.NET uses a unique **Dual-Pipeline** system to solve the "over-filtering" problem common in dynamic query libraries. It decouples **Root Filtering** from **Data Shaping**.

### Pipeline 1: Root Filtering (WHERE)
This pipeline determines which root entities (e.g., *Customers*) appear in the results. If a customer doesn't match the criteria, they are excluded entirely.
- **Parameter**: `filter`, `query`
- **Effect**: `IQueryable.Where(...)`

### Pipeline 2: Data Shaping (Filtered Includes)
This pipeline determines what related data is fetched for the root entities and how that related data is filtered. It does **not** affect the count of root entities returned.
- **Parameter**: `include`
- **Effect**: `IQueryable.Include(x => x.Orders.Where(...))`

> [!TIP]
> **Example**: If you filter `orders.any(total > 100)` in the root filter, you get customers who have such an order. If you then `include=orders(total > 100)`, the resulting customer objects will only contain the matching orders in their collection, rather than all orders.

## Validation Engine

Security and correctness are ensured by the **Validation Engine**. It runs against your EF Core metadata to verify:
1.  **Existence**: Do the fields exist on the model?
2.  **Type Compatibility**: Is the user trying to compare a `DateTime` to an `Integer`?
3.  **Security Rules**: Are the requested fields allowed based on the `AllowedFields` whitelist?

The validation happens automatically when you call `ApplyValidatedQueryOptions(options)`, throwing a `QueryValidationException` on failure.

