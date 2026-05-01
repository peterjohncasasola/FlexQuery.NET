using FlexQuery.NET.Tests.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Fixtures;

/// <summary>
/// EF Core InMemory DbContext used by all test classes.
/// Each test should use a unique database name to ensure isolation.
/// </summary>
public class TestDbContext : DbContext
{
    public DbSet<TestEntity> Entities => Set<TestEntity>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.City).IsRequired();
        });
    }

    // ── Factory helpers ──────────────────────────────────────────────────

    /// <summary>Creates a fresh in-memory context with a unique database name.</summary>
    public static TestDbContext Create(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var ctx = new TestDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>Creates a pre-seeded context with standard test data.</summary>
    public static TestDbContext CreateSeeded(string? dbName = null)
    {
        var ctx = Create(dbName ?? Guid.NewGuid().ToString());
        ctx.Entities.AddRange(SeedData());
        ctx.SaveChanges();
        return ctx;
    }

    public static IReadOnlyList<TestEntity> SeedData()
    {
        return new List<TestEntity>
        {
            new() { Id = 1, Name = "Alice Johnson", Age = 30, City = "New York",  CreatedAt = new DateTime(2023, 1, 1), Status = Models.Status.Active,
                    Profile = new Profile { Id = 1, Bio = "Developer" }, 
                    Orders = [
                        new Order { Id = 101, Total = 50.0m, Status = "Shipped",
                            OrderItems = [new OrderItem { Id = 1, Quantity = 2, Price = 25.0m }, new OrderItem { Id = 2, Quantity = 1, Price = 10.0m }] },
                        new Order { Id = 102, Total = 25.0m, Status = "Pending",
                            OrderItems = [new OrderItem { Id = 3, Quantity = 3, Price = 5.0m }] }
                    ] },
            new() { Id = 2, Name = "Bob Smith",     Age = 25, City = "London",    CreatedAt = new DateTime(2023, 2, 1), Status = Models.Status.Inactive,
                    Profile = new Profile { Id = 2, Bio = "Designer" },
                    Orders = [new Order { Id = 103, Total = 100.0m }] },
            new() { Id = 3, Name = "Carol White",   Age = 35, City = "New York",  CreatedAt = new DateTime(2023, 3, 1), Status = Models.Status.Pending,
                    Profile = new Profile { Id = 3, Bio = "Manager" },
                    Orders = [] },
            new() { Id = 4, Name = "David Brown",   Age = 28, City = "Paris",     CreatedAt = new DateTime(2023, 4, 1), Status = Models.Status.Active,
                    Profile = null,
                    Orders = [new Order { Id = 104, Total = 200.0m }] },
            new() { Id = 5, Name = "Eve Davis",     Age = 22, City = "London",    CreatedAt = new DateTime(2023, 5, 1), Status = Models.Status.Inactive },
            new() { Id = 6, Name = "Frank Miller",  Age = 40, City = "Berlin",    CreatedAt = new DateTime(2023, 6, 1), Status = Models.Status.Active   },
            new() { Id = 7, Name = "Grace Wilson",  Age = 19, City = "Paris",     CreatedAt = new DateTime(2023, 7, 1), Status = Models.Status.Pending  },
            new() { Id = 8, Name = "Hank Moore",    Age = 45, City = "New York",  CreatedAt = new DateTime(2023, 8, 1), Status = Models.Status.Active   },
            new() { Id = 9, Name = "Ivy Taylor",    Age = 33, City = "Berlin",    CreatedAt = new DateTime(2023, 9, 1), Status = Models.Status.Inactive },
            new() { Id =10, Name = "Jack Anderson", Age = 27, City = "London",    CreatedAt = new DateTime(2023,10, 1), Status = Models.Status.Active   },
        };
    }
}
