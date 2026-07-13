# Dapper Relationship Queries

## Overview

When FlexQuery generates SQL for queries that involve navigation properties (includes, filtered includes), it builds the appropriate JOIN structure automatically. This page explains how relationship queries translate to SQL in the Dapper provider.

## How Relationships Are Discovered

The `SqlTranslator` uses the mapping registry to resolve relationship metadata at translation time. For each navigation property encountered in the `QueryOptions.Includes` or `QueryOptions.FilteredIncludes`, the translator looks up the registered `RelationshipMapping` and `JoinInfo` to determine the correct JOIN type, table name, and join predicate.

## Simple Includes → LEFT JOIN

When a client requests related data via `?include=Orders`, the translator generates a `LEFT JOIN`:

```
?include=Orders

→ SELECT [Users].[Id], [Users].[Name],
         [Orders].[Id] AS [Orders_Id], [Orders].[Total] AS [Orders_Total]
  FROM [Users]
  LEFT JOIN [Orders] ON [Users].[Id] = [Orders].[UserId]
```

Multiple includes generate multiple JOINs:
```
?include=Orders,Profile

→ SELECT ...
  FROM [Users]
  LEFT JOIN [Orders] ON [Users].[Id] = [Orders].[UserId]
  LEFT JOIN [Profiles] ON [Users].[Id] = [Profiles].[UserId]
```

## Filtered Includes → LEFT JOIN + WHERE-like condition

Filtered includes translate the inline FQL filter into a WHERE condition applied inside the JOIN predicate (or as an additional WHERE clause scoped to that join):

```
?include=Orders(Status = 'Active' AND Total > 100)

→ SELECT ...
  FROM [Users]
  LEFT JOIN [Orders]
    ON [Users].[Id] = [Orders].[UserId]
    AND [Orders].[Status] = @p0
    AND [Orders].[Total] > @p1
```

Parameters: `{ "@p0": "Active", "@p1": 100 }`

## Nested Relationships

Dot-chained includes generate nested JOINs:

```
?include=Orders.OrderItems

→ SELECT ...
  FROM [Users]
  LEFT JOIN [Orders] ON [Users].[Id] = [Orders].[UserId]
  LEFT JOIN [OrderItems] ON [Orders].[Id] = [OrderItems].[OrderId]
```

## EXISTS / NOT EXISTS (Relationship Filtering)

When a client filters using a relationship condition (e.g., "users who have at least one active order"), the `SqlExistsTranslator` generates a correlated `EXISTS` subquery:

```
?filter=Orders.Status eq 'Active'

→ WHERE EXISTS (
    SELECT 1 FROM [Orders]
    WHERE [Orders].[UserId] = [Users].[Id]
    AND [Orders].[Status] = @p0
  )
```

Negated relationship filters use `NOT EXISTS`:
```
?filter=Orders neq exists

→ WHERE NOT EXISTS (
    SELECT 1 FROM [Orders]
    WHERE [Orders].[UserId] = [Users].[Id]
  )
```

## COUNT on Relationships

The `SqlCountTranslator` handles relationship count conditions:

```
?filter=Orders.count gt 5

→ WHERE (
    SELECT COUNT(1) FROM [Orders]
    WHERE [Orders].[UserId] = [Users].[Id]
  ) > @p0
```

## Flat Projection Mode

When `ProjectionMode` is `Flat` or `FlatMixed`, the translator uses `BuildFlatSelectClause()` which generates a single-query flat result with aliased columns:

```
?select=Id,Name,Orders.Id,Orders.Total&mode=flat

→ SELECT [Users].[Id], [Users].[Name],
         [Orders].[Id] AS [Orders_Id], [Orders].[Total] AS [Orders_Total]
  FROM [Users]
  LEFT JOIN [Orders] ON [Users].[Id] = [Orders].[UserId]
```

The result is a flat row set. FlexQuery returns each row as-is — no hydration into nested objects.

## Row Hydration for Includes

When using standard (non-flat) includes, Dapper returns dynamic rows which are then passed through `DapperRowHydrator.HydrateIncludes<T>()`. This groups the flat result rows by the root entity's primary key and constructs nested collection properties.

```csharp
// This is handled automatically by FlexQueryAsync
// The SQL dialect is auto-detected from the DbConnection type.
var result = await connection.FlexQueryAsync<User>(parameters);

// result.Data[0].Orders is populated from the JOIN result
```

## Performance Considerations

- **LEFT JOINs with collections** can produce Cartesian products when multiple one-to-many joins are stacked. If you join `Orders` and `Tags` independently, you get `Orders × Tags` rows per user. Use flat projection mode carefully with multiple collection includes.
- **EXISTS subqueries** are generally more efficient than JOINs for relationship-based filtering where you only need the parent entity
- **`MaxFieldDepth`** limits the depth of relationship traversal, which bounds the number of generated JOINs

## Security Considerations

- Always configure `AllowedIncludes` to control which navigation properties clients can load
- Deep include chains (`Orders.Items.Product.Category`) generate many JOINs and can be expensive — use `MaxFieldDepth` to prevent excessive traversal

## Best Practices

1. **Use `AllowedIncludes`** to whitelist which relationships clients can request
2. **Prefer flat projection** (`?mode=flat`) for simple tabular data needs — it generates a single, clean SQL query
3. **Use EXISTS filters** when you only need to check relationship existence, not load the related data
4. **Set `MaxFieldDepth`** to prevent deep traversal chains
5. **Test with `ToSqlPreview()`** equivalent — access `SqlCommand.Sql` directly from the translator to verify generated SQL

## Related Features

- [SQL Generation](/providers/dapper/sql-generation) — How the translator assembles clauses
- [Conventions](/providers/dapper/conventions) — How relationship metadata is discovered
- [Dialects](/providers/dapper/dialects) — How JOINs differ across databases
- [Include Filtering](/guide/include-filtering) — The include filter syntax
