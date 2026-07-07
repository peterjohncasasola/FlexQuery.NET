using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Fixtures;

public sealed class SqlProjectionDbContext : DbContext, IDisposable
{
    private readonly SqliteConnection _connection;

    public DbSet<SqlCustomer> Customers => Set<SqlCustomer>();
    public DbSet<SqlAddress> Addresses => Set<SqlAddress>();
    public DbSet<SqlOrder> Orders => Set<SqlOrder>();
    public DbSet<SqlOrderItem> OrderItems => Set<SqlOrderItem>();

    private SqlProjectionDbContext(DbContextOptions<SqlProjectionDbContext> options, SqliteConnection connection)
        : base(options)
    {
        _connection = connection;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlCustomer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Email).IsRequired();
            entity.HasOne(x => x.Address)
                .WithOne(x => x.Customer)
                .HasForeignKey<SqlAddress>(x => x.CustomerId);
            entity.HasMany(x => x.Orders)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<SqlAddress>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.City).IsRequired();
        });

        modelBuilder.Entity<SqlOrder>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).IsRequired();
            entity.Property(x => x.Total).HasColumnType("NUMERIC");
            entity.HasMany(x => x.Items)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<SqlOrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sku).IsRequired();
        });
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public static SqlProjectionDbContext CreateSeeded()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqlProjectionDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new SqlProjectionDbContext(options, connection);
        context.Database.EnsureCreated();

        if (!context.Customers.Any())
        {
            var alice = new SqlCustomer
            {
                Id = 1,
                Name = "Alice",
                Email = "alice@example.com",
                Address = new SqlAddress { Id = 100, City = "Zurich" },
                Orders =
                [
                    new SqlOrder
                    {
                        Id = 10,
                        Number = "SO-001",
                        Total = 125.50m,
                        OrderDate = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                        Items =
                        [
                            new SqlOrderItem { Id = 1000, Sku = "SKU-AAA" },
                            new SqlOrderItem { Id = 1001, Sku = "SKU-BBB" }
                        ]
                    },
                    new SqlOrder
                    {
                        Id = 11,
                        Number = "SO-002",
                        Total = 45.00m,
                        OrderDate = new DateTime(2025, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                        Items =
                        [
                            new SqlOrderItem { Id = 1002, Sku = "SKU-CCC" }
                        ]
                    }
                ]
            };

            var bob = new SqlCustomer
            {
                Id = 2,
                Name = "Bob",
                Email = "bob@example.com",
                Address = new SqlAddress { Id = 101, City = "Athens" },
                Orders =
                [
                    new SqlOrder
                    {
                        Id = 12,
                        Number = "SO-003",
                        Total = 99.00m,
                        OrderDate = new DateTime(2025, 1, 3, 8, 0, 0, DateTimeKind.Utc)
                    }
                ]
            };

            var bobTwo = new SqlCustomer
            {
                Id = 3,
                Name = "Bob",
                Email = "bob2@example.com",
                Address = new SqlAddress { Id = 102, City = "Zurich" },
                Orders =
                [
                    new SqlOrder
                    {
                        Id = 13,
                        Number = "SO-004",
                        Total = 10.00m,
                        OrderDate = new DateTime(2025, 1, 4, 8, 0, 0, DateTimeKind.Utc)
                    }
                ]
            };

            context.Customers.AddRange(alice, bob, bobTwo);
            context.SaveChanges();
        }

        return context;
    }
}
