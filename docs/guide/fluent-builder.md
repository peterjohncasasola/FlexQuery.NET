# Fluent Query Builder

## Overview

FlexQuery.NET v4 introduces a fully typed, fluent API for constructing queries programmatically. The Fluent Query Builder allows you to build `QueryOptions` objects directly in C# without concatenating strings or parsing JSON.

## Why this feature exists

While FlexQuery's string-based DSL and JSON formats are excellent for HTTP APIs and frontend-to-backend communication, they are cumbersome to use when constructing queries dynamically *within* your backend code (e.g., service-to-service communication, background workers, or dynamic repository methods). The Fluent Builder provides a type-safe, discoverable alternative.

## Basic Usage

Note: v4 replaces string-based aggregate function names with the strongly-typed `AggregateFunction` enum. Use the dedicated builder methods (`.Sum()`, `.Count()`, `.Avg()`, `.Min()`, `.Max()`) instead of `.Custom()`.

The entry point for the fluent API is the static `Query.Create()` method.

```csharp
using FlexQuery.NET.Builders.Fluent;

var options = Query.Create()
    .Filter(f => f
        .Equal("Status", "Active")
        .GreaterThan("Age", 18))
    .Sort(s => s.Ascending("CreatedAt"))
    .Page(1, pageSize: 50)
    .Select("Id", "Name", "Email")
    .Build();

// options is now a fully formed QueryOptions object
var result = await _context.Users.FlexQueryAsync(options);
```

## Filtering

The `Filter` method accepts an action that configures a `FilterGroupBuilder`. You can build complex, nested logical groups.

### Simple Conditions

```csharp
var options = Query.Create()
    .Filter(f => f.Equal("Category", "Electronics"))
    .Build();
```

### Logical Operators

Conditions are implicitly ANDed. Use `.And(...)` and `.Or(...)` for nested groups.

```csharp
var options = Query.Create()
    .Filter(f => f
        .Equal("Status", "Active")
        .GreaterThan("Price", 100))
    .Build();
```

### Nested Groups

For grouped logic (like parentheses in SQL), use `.And(...)` or `.Or(...)` with a lambda:

```csharp
// Generates: Status eq 'Active' AND (Role eq 'Admin' OR Role eq 'Manager')
var options = Query.Create()
    .Filter(f => f
        .Equal("Status", "Active")
        .And(g => g
            .Equal("Role", "Admin")
            .Or(o => o.Equal("Role", "Manager"))))
    .Build();
```

### All Filter Operators

```csharp
var options = Query.Create()
    .Filter(f => f
        .Equal("A", "x")
        .NotEqual("B", "y")
        .GreaterThan("C", 1)
        .GreaterThanOrEqual("D", 2)
        .LessThan("E", 3)
        .LessThanOrEqual("F", 4)
        .Contains("G", "sub")
        .StartsWith("H", "pre")
        .EndsWith("I", "suf")
        .In("J", "a", "b", "c")
        .NotIn("K", "d", "e")
        .IsNull("L")
        .IsNotNull("M")
        .Between("N", 10, 20))
    .Build();
```

## Sorting

You can chain `.Sort()` calls using a `SortBuilder` lambda to apply secondary and tertiary sorts.

```csharp
var options = Query.Create()
    .Sort(s => s.Ascending("Category.Name"))
    .Sort(s => s.Descending("Price"))
    .Build();
```

## Paging

Configure offset paging using the `.Page()` method.

```csharp
var options = Query.Create()
    .Page(pageNumber: 2, pageSize: 20)
    .Build();
```

## Includes (Expand)

You can specify navigation properties to eager load, along with optional nested filters for filtered includes.

```csharp
var options = Query.Create()
    .Expand(e => e.Path("Orders", f => f.GreaterThan("Total", 100)))
    .Build();
```

## Aggregations

Configure server-side aggregations safely:

```csharp
var options = Query.Create()
    .Aggregate(a => a
        .Sum("Amount", "TotalAmount")
        .Count("Id", "OrderCount")
        .Count("TotalCount")
        .Avg("Price", "AvgPrice")
        .Min("Date", "Earliest")
        .Max("Date", "Latest"))
    .Build();
```

## When to use

* **Service Layer:** When one backend service needs to query another using FlexQuery conventions.
* **Background Jobs:** When a worker process needs to generate a dynamic report.
* **Unit Testing:** When writing integration tests for your FlexQuery endpoints, it's safer to use the builder than to hardcode DSL strings in your tests.