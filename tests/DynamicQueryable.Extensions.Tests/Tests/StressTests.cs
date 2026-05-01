using System.Diagnostics;
using DynamicQueryable.Extensions;
using DynamicQueryable.Parsers;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DynamicQueryable.Tests.Tests;

public class StressTests : IDisposable
{
    private readonly TestDbContext _db;

    public StressTests()
    {
        // Seed a large dataset for stress testing
        _db = TestDbContext.Create("StressTestDb");
        SeedLargeDataset(_db);
    }

    public void Dispose() => _db.Dispose();

    private void SeedLargeDataset(TestDbContext context)
    {
        if (context.Entities.Any()) return;

        var entities = new List<Models.TestEntity>();
        var random = new Random(42);

        for (int i = 1; i <= 1000; i++) // 1000 entities for reasonable test time, increase if needed
        {
            var entity = new Models.TestEntity
            {
                Id = i,
                Name = $"User {i}",
                Age = random.Next(18, 80),
                City = i % 2 == 0 ? "New York" : "London",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 1000)),
                Status = (Models.Status)random.Next(0, 3),
                Orders = new List<Models.Order>()
            };

            for (int j = 1; j <= 5; j++)
            {
                var order = new Models.Order
                {
                    Id = (i * 10) + j,
                    Total = (decimal)(random.NextDouble() * 1000),
                    Status = j % 2 == 0 ? "Shipped" : "Pending",
                    OrderItems = new List<Models.OrderItem>()
                };

                for (int k = 1; k <= 3; k++)
                {
                    order.OrderItems.Add(new Models.OrderItem
                    {
                        Id = (order.Id * 10) + k,
                        Quantity = random.Next(1, 10),
                        Price = (decimal)(random.NextDouble() * 100)
                    });
                }
                entity.Orders.Add(order);
            }
            entities.Add(entity);
        }

        context.Entities.AddRange(entities);
        context.SaveChanges();
    }

    [Fact]
    public async Task HighLoad_ConcurrentRequests_ShouldSucceedWithoutErrors()
    {
        // Arrange
        int concurrentRequests = 100; // Simulating 100 concurrent requests for test efficiency
        var tasks = new List<Task>();
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> 
        { 
            { "query", "orders.any(orderItems.any(quantity > 5))" },
            { "select", "id,name,orders.total" } 
        };
        var options = QueryOptionsParser.Parse(dict);

        var sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                // Each request uses a new context or the same?
                // In a real app, it's a new scoped DbContext. Let's simulate that safely
                using var scopeDb = TestDbContext.Create("StressTestDb"); // Use same in-memory DB name
                var result = await scopeDb.Entities
                    .ApplyFilter(options)
                    .ApplySelect(options)
                    .ToListAsync();
                
                result.Should().NotBeNull();
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        // The average execution time per request should be reasonably low.
        // We assert the total time for 100 requests is under a threshold (e.g. 5 seconds for in-memory)
        sw.ElapsedMilliseconds.Should().BeLessThan(60000, "100 concurrent deep queries should execute within 60 seconds on InMemory DB.");
    }
}
