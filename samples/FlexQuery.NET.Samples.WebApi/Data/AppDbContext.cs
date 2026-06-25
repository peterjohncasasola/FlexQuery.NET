using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.HasMany(c => c.Orders)
                  .WithOne(o => o.Customer)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount).HasConversion<double>(); // Sqlite decimal support helper
        });
    }
}
