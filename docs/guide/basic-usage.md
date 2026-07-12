# Basic Usage Guide

## Overview

FlexQuery.NET uses a consistent, human-readable DSL (Domain Specific Language) for dynamic querying. This guide provides a rapid introduction to the standard formats for filtering, sorting, paging, and projection.

> **Note:** Examples using `FlexQueryAsync` require the `FlexQuery.NET.EntityFrameworkCore` or `FlexQuery.NET.Dapper` package. The core `FlexQuery.NET` package provides synchronous `FlexQuery` for advanced scenarios.

## Why this feature exists

When building APIs, there is a constant tension between backend rigidity and frontend flexibility. The FlexQuery DSL exists to provide a standardized, secure, and easily-parsable syntax that frontends can use to request exact data shapes without forcing backend developers to write custom SQL or LINQ for every view.

## When to use

- Read this guide to understand the fundamental URL syntax for FlexQuery GET requests.
- Share this guide with frontend engineers so they understand how to construct query strings.

---

## Filtering

Filtering allows you to restrict the results based on property values. The standard format for any operation is `Field:Operator:Value`.

### Simple Filters
- **Equals**: `Name:eq:John`
- **Contains**: `Name:contains:John`
- **Greater Than**: `Price:gt:100`
- **In Collection**: `Status:in:Active,Pending`

### Multiple Filters
Multiple filters are combined using the **AND** operator (`&`), which must be URL-encoded as `%26` in HTTP requests.

`?filter=Status:eq:Active%26Price:gt:100`

### Nested Properties
You can filter on nested navigation properties using dot notation. FlexQuery automatically handles generating the underlying SQL `JOIN` or EF Core `Include` logic.

`?filter=Category.Name:eq:Electronics`

---

## Sorting

Sorting controls the order of the returned items. You can specify ascending (`asc`) or descending (`desc`).

- **Ascending**: `?sort=Name:asc`
- **Descending**: `?sort=Price:desc`
- **Multiple Columns**: Use a comma to separate multiple sort directives.
  `?sort=Category.Name:asc,Price:desc`

---

## Paging

FlexQuery supports standard offset paging parameters as well as high-performance keyset paging.

- **Page**: The current page number (1-based). `?page=1`
- **PageSize**: Number of items per page. `?pageSize=20`

### Result Shape

When paging is used, the result includes a standardized pagination envelope (the `QueryResult<T>` contract):

```json
{
  "totalCount": 150,
  "resultCount": 20,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "data": [
    // ... 20 items ...
  ],
  "nextCursorToken": null
}
```

---

## Projection (Select)

Projection allows you to specify exactly which fields should be returned. This reduces database I/O, network bandwidth, and memory allocation.

**Basic Select:**
`?select=Id,Name,Price`

**Nested Select:**
`?select=Id,Name,Category.Name`

> [!IMPORTANT]
> When using `select`, only the requested fields will be populated in the result object. All other fields will be null or default values in the JSON response.

---

## Unified Execution

In FlexQuery v4, all these features are applied in a single unified pipeline. You don't need to manually string together `.Where()`, `.OrderBy()`, or `.Select()` clauses.

```csharp
[HttpGet]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters)
{
    // Execute everything in one pass
    var result = await _context.Customers.FlexQueryAsync(parameters, options => 
    {
        // Enforce your security rules
        options.AllowedFields = ["Id", "Name", "Price", "Category.Name", "Status"];
    });

    return Ok(result);
}
```

### HTTP POST Requests

If your query is too large for a URL query string, you can use the `FlexQueryRequest` model to accept the query via a JSON POST body. The properties are exactly the same as `FlexQueryParameters`.

```csharp
[HttpPost("query")]
public async Task<IActionResult> QueryUsers([FromBody] FlexQueryRequest request)
{
    var options = request.ToQueryOptions();
    var result = await _context.Customers.FlexQueryAsync(options, exec => 
    {
        exec.AllowedFields = new HashSet<string> { "Id", "Name", "Price", "Status" };
    });

    return Ok(result);
}
```

**JSON Payload:**
```json
{
  "filter": "Status:eq:Active",
  "sort": "Name:asc",
  "page": 1,
  "pageSize": 20
}
```

This single call handles:
1. Parsing the query string.
2. Validating the requested fields against your `AllowedFields` policy.
3. Building the Expression Tree or ADO.NET SQL Command.
4. Counting the total records (if requested).
5. Executing the query against the database and paginating.

## Best Practices

- **URL Encode:** Always remind frontend developers to use `encodeURIComponent()` (in JavaScript/TypeScript) on their filter strings. The `&` character will break HTTP routing if it is not encoded as `%26`.
- **Use Paging Defaults:** Always specify a `DefaultPageSize` and `MaxPageSize` in your execution options to prevent accidental `SELECT * FROM Table` scenarios.
