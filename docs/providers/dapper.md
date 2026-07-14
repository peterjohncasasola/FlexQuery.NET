# Dapper Integration

`FlexQuery.NET.Dapper` is a powerful extension that translates `QueryOptions` directly into raw, parameterized SQL queries, allowing you to use FlexQuery's dynamic filtering, sorting, and projection without the overhead of Entity Framework Core.

## Registration

To use the Dapper extensions, register them in your DI container and specify your target SQL dialect:

```csharp
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;

builder.Services.AddFlexQueryDapper(opts => {
    // Specify the database dialect (SqlServer, Postgres, Sqlite, etc.)
    opts.Dialect = new SqlServerDialect(); 
});
```

## SQL Translation

The core of the Dapper package is the `SqlTranslator`. It safely converts expression trees and query options into parameterized SQL strings, completely avoiding SQL injection.

```csharp
var options = // ... build or parse QueryOptions
var query = new SqlTranslator(dialect)
    .Select("Users")
    .Where(options.Filter)
    .OrderBy(options.Sort)
    .Build();

// Execute using Dapper
var results = await connection.QueryAsync<User>(query.Sql, query.Parameters);
```

## Flat Projection Mode

When dealing with deep projection requests (e.g. `$select=Id,Name,Orders.Id,Orders.Total`), the Dapper engine supports **Flat Projection**. Instead of returning nested objects, it dynamically generates SQL `LEFT JOIN` clauses and returns a flattened result set.

```csharp
// Example generated SQL for flat projection:
// SELECT t0.Id, t0.Name, t1.Id AS Orders_Id, t1.Total AS Orders_Total 
// FROM Users t0 
// LEFT JOIN Orders t1 ON t0.Id = t1.UserId
```

This mode is heavily optimized and prevents N+1 query issues by fetching all required data in a single round-trip.
