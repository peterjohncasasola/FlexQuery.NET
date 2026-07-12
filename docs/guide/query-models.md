# Query Models

FlexQuery.NET provides two primary request models for handling dynamic queries from your API. While both models use the same underlying engine, they are designed for different levels of complexity and API surface area.

## FlexQueryRequest (Full Feature)

`FlexQueryRequest` is the comprehensive model that exposes every capability the library offers. Use this when your consumers need full control over the query pipeline.

- **Capabilities**: 
  - `Filter`, `Sort`, `Select`, `Include`
  - `GroupBy`, `Having`, `Aggregate`
  - `Mode` (Projection control: Nested, Flat, etc.)
  - `Expand` (Deep, filtered navigation expansion)
  - `IncludeCount`, `Distinct`
- **When to use**: 
  - Advanced APIs requiring deep data shaping.
  - Internal administrative systems.
  - Scenarios where aggregations and complex projections are required from the client.

## FlexQueryParameters (Simplified)

`FlexQueryParameters` is a lightweight, opinionated DTO designed for clean, public-facing APIs. It focuses on the most common operations while maintaining a minimal API surface.

- **Focus**:
  - `Filter`, `Sort`, `Select`
  - `Page`, `PageSize` (Pagination)
- **Key Advantages**:
  - **Swagger-Friendly**: Includes built-in XML comments and examples that automatically populate Swagger UI.
  - **Easier to Use**: Prevents overwhelming frontend developers with advanced parameters they may not need.
  - **Cleaner API**: Keeps your URL query strings tidy and predictable.

## Recommendation

As a general rule:
1. **Start with `FlexQueryParameters`**. It covers 90% of standard API filtering and paging needs.
2. **Upgrade to `FlexQueryRequest`** only when you specifically need features like explicit Grouping, Aggregates, or complex Projection modes.

## Key Note

Both models map to the same internal `QueryOptions` engine. Using the simplified model does not mean you lose performance or filtering power—it simply hides advanced configuration parameters from the client-facing contract.

## Example Usage

### Simple Usage (Recommended)
```csharp
using FlexQuery.NET.Models;
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    var options = parameters.ToQueryOptions();
    return Ok(await _context.Products.FlexQueryAsync(options));
}
```

### Advanced Usage
```csharp
[HttpGet]
public async Task<IActionResult> Search([FromQuery] FlexQueryParameters parameters)
{
    var options = parameters.ToQueryOptions();
    // Allows client to specify includes, GroupBy, and Having
    return Ok(await _context.Orders.FlexQueryAsync(options));
}
```
