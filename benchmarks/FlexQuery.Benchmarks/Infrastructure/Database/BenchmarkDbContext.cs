using FlexQuery.Benchmarks.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.Benchmarks.Infrastructure.Database;

/// <summary>
/// EF Core DbContext for benchmark scenarios.
/// Uses InMemory provider for isolation and reproducibility.
/// For SQL Server benchmarks, swap the provider in BenchmarkDbContextFactory.
/// </summary>
public class BenchmarkDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Payment> Payments => Set<Payment>();

    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasMany(u => u.Orders).WithOne(o => o.User).HasForeignKey(o => o.UserId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
            e.HasMany(o => o.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);
        });

        modelBuilder.Entity<Product>(e => e.HasKey(p => p.Id));
        modelBuilder.Entity<Payment>(e => e.HasKey(p => p.Id));
    }
}

/// <summary>
/// Factory for creating benchmark DbContext instances.
/// </summary>
public static class BenchmarkDbContextFactory
{
    private static readonly string DbName = $"FlexQueryBenchmarks_{Guid.NewGuid():N}";

    public static BenchmarkDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FlexQueryBenchmarks;Trusted_Connection=True;")
            .Options;

        return new BenchmarkDbContext(options);
    }

    /// <summary>
    /// Creates a fresh context with a unique database name (for isolation between benchmark runs).
    /// </summary>
    public static BenchmarkDbContext CreateFresh()
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseInMemoryDatabase($"FlexQueryBench_{Guid.NewGuid():N}")
            .Options;

        return new BenchmarkDbContext(options);
    }
}
