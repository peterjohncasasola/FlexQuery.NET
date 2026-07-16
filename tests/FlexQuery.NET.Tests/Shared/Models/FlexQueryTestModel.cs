using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Metadata;

namespace FlexQuery.NET.Tests.Shared.Models;

public static class SharedFlexQueryModel
{
    private static FlexQueryModel CreateModel()
    {
        var builder = new ModelBuilder();

        builder.Entity<Customer>()
            .ToTable("Customers")
            .HasOne(c => c.Address)
            .WithForeignKey("CustomerId");

        builder.Entity<Customer>()
            .HasMany(c => c.Orders)
            .WithForeignKey("CustomerId");

        builder.Entity<Order>()
            .ToTable("Orders")
            .HasMany(o => o.OrderItems)
            .WithForeignKey("OrderId");

        builder.Entity<OrderItem>()
            .ToTable("OrderItems");
        
        builder.Entity<User>()
            .ToTable("Users")
            .HasMany(u => u.Roles).WithForeignKey("UserId");
        
        builder.Entity<Role>()
            .ToTable("Roles")
            .HasMany(r => r.Permissions).WithForeignKey("RoleId");
        
        builder.Entity<Permission>()
            .ToTable("Permissions");
        
        builder.Entity<Employee>()
            .ToTable("Employees")
            .HasOne(e => e.Manager).WithForeignKey("ManagerId");
        
        return builder.Build();
    }

    public static FlexQueryModel Instance => CreateModel();
}