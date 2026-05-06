> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Performance Optimization

FlexQuery.NET is built for high-performance production environments. It includes several automatic and manual optimizations.

## SQL Translation Efficiency

- **EXISTS Translation**: Deeply nested collection filters (e.g., `orders.any(orderItems.any(quantity > 5))`) are translated by EF Core into nested SQL `EXISTS` clauses. This is much more efficient than fetching intermediate records into memory.
- **Single-Trip Projection**: When using `ApplySelect`, all filtered includes and property selections are merged into a single SQL `SELECT` statement, minimizing database round-trips.

## Paging & Sorting Fix

Relational databases require a deterministic order for consistent paging. If a client requests `page` or `skip` without an explicit `sort`, FlexQuery.NET automatically injects a default `OrderBy` (usually on the primary key `Id`). This prevents EF Core runtime errors and ensures users don't see duplicate records across pages.

## Memory Optimization

Filtering is strictly applied **before** any dynamic projection. This ensures that only the required records are processed and that the memory footprint of the materialized anonymous objects is kept to a minimum.

## Monitoring with EF Core Interceptors

You can monitor the performance of dynamic queries by using EF Core `DbCommandInterceptor`.

```csharp
public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger _logger;
    public SlowQueryInterceptor(ILogger logger) => _logger = logger;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (eventData.Duration.TotalMilliseconds > 500)
        {
            _logger.LogWarning("Slow Dynamic Query ({Duration}ms):\n{CommandText}", 
                eventData.Duration.TotalMilliseconds, command.CommandText);
        }
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
```

Register it in your `DbContext` setup:
```csharp
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(new SlowQueryInterceptor(logger));
});
```

