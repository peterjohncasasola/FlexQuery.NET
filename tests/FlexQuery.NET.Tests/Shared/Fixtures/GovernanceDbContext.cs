using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

public sealed class GovernanceDbContext : SharedTestDbContext
{
    private readonly SqliteConnection _connection;

    private GovernanceDbContext(DbContextOptions<SharedTestDbContext> options, SqliteConnection connection)
        : base(options)
    {
        _connection = connection;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.SSN).IsRequired();
            entity.Property(x => x.Salary).HasColumnType("NUMERIC");
            entity.HasMany(x => x.Orders)
                .WithOne()
                .HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Total).HasColumnType("NUMERIC");
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.Category);
        });
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection.Dispose();
    }

    public static GovernanceDbContext CreateSeeded()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new GovernanceDbContext(options, connection);
        context.Database.EnsureCreated();

        if (!context.Customers.Any())
        {
            context.Customers.AddRange(
                new Customer
                {
                    Id = 1,
                    Name = "Alice",
                    SSN = "111-11-1111",
                    Salary = 50000m,
                    Orders =
                    [
                        new() { Id = 1, CustomerId = 1, Total = 100m, Status = "Shipped", Category = "Electronics" },
                        new() { Id = 2, CustomerId = 1, Total = 200m, Status = "Pending", Category = "Books" }
                    ]
                },
                new Customer
                {
                    Id = 2,
                    Name = "Bob",
                    SSN = "222-22-2222",
                    Salary = 60000m,
                    Orders =
                    [
                        new() { Id = 3, CustomerId = 2, Total = 300m, Status = "Shipped", Category = "Electronics" }
                    ]
                },
                new Customer
                {
                    Id = 3,
                    Name = "Charlie",
                    SSN = "333-33-3333",
                    Salary = 70000m,
                    Orders =
                    [
                        new() { Id = 4, CustomerId = 3, Total = 400m, Status = "Cancelled", Category = "Books" }
                    ]
                });

            context.SaveChanges();
        }

        return context;
    }
}