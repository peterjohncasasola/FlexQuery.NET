> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Projection & Shaping

Projection allows you to control exactly which fields are returned from your API, reducing payload size and improving performance.

## Select

Use the `select` parameter to list the fields you want to retrieve.

`?select=id,name,email`

### Aliases (`as`)

You can rename properties in the output dynamic object using the `as` keyword.

`?select=id as customerId, name, emailAddress as contactEmail`

Aliases work at any level of nesting:
`?select=id,orders.status as orderStatus,orders.orderItems.productName as product`

## Include (Eager Loading)

While `select` creates a new dynamic object, `include` is used to expand related navigation properties while keeping the original entity structure.

`?include=orders,profile`

### Filtered Includes

FlexQuery.NET allows you to filter the related collections that are being included. This is part of the **Dual-Pipeline** system.

`?include=orders(status = 'Shipped')`

You can chain includes and even filter at multiple levels:
`?include=orders(total > 100).orderItems(sku = 'SKU-001')`

## Exclusive Selection

If you provide a specific `select` path for a navigation, the library will **only** project those fields for that navigation, overriding the default behavior of including all scalar properties.

**Query:**
`?include=orders&select=id,orders.number`

**Result:**
The `orders` collection will contain objects having only `id` and `number` properties.

## Merged Projection Mode

When using `ApplySelect` or `ToProjectedQueryResultAsync`, the library automatically merges **Filtered Includes** and **Select** into a single optimized `Select()` expression tree. This ensures:

1.  Only requested columns are fetched from the database.
2.  Related data is filtered at the database level.
3.  The final result is a clean, minimal anonymous object structure.

