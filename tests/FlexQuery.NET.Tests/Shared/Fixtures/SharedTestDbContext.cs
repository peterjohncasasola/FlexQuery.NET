using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

public class SharedTestDbContext(DbContextOptions<SharedTestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.City).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.HasOne(x => x.Profile)
                .WithOne()
                .HasForeignKey<Profile>(p => p.Id);
            e.HasMany(x => x.Orders)
                .WithOne()
                .HasForeignKey("CustomerId");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired();
            entity.Property(c => c.Status).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();
            entity.HasOne(c => c.Profile)
                .WithOne()
                .HasForeignKey<Profile>(p => p.Id);
            entity.HasOne(c => c.Address)
                .WithOne()
                .HasForeignKey<Address>(a => a.CustomerId);
            entity.HasMany(c => c.Addresses)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId);
            entity.HasMany(c => c.Orders)
                .WithOne(o => o.Customer)
                .HasForeignKey(o => o.CustomerId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Number).IsRequired();
            entity.Property(o => o.Status).IsRequired();
            entity.Property(o => o.OrderDate).IsRequired();
            entity.HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId);
            entity.HasMany(o => o.OrderItems)
                .WithOne()
                .HasForeignKey("OrderId");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SKU).IsRequired();
            entity.Property(p => p.Name).IsRequired();
            entity.Property(p => p.Price).IsRequired();
            entity.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired();
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.Id);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.CustomerId).IsRequired();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.UserName).IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired();
        });

        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users);

        modelBuilder.Entity<Role>()
            .HasMany(r => r.Permissions)
            .WithMany(p => p.Roles);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Email);
            entity.HasOne(x => x.Address)
                .WithOne()
                .HasForeignKey<Address>(x => x.CustomerId);
            entity.HasMany(x => x.Orders)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.City).IsRequired();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).IsRequired();
            entity.Property(x => x.Total).HasColumnType("NUMERIC");
            entity.HasMany(x => x.OrderItems)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sku).IsRequired();
        });
    }

    public static SharedTestDbContext Create(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var ctx = new SharedTestDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static SharedTestDbContext CreateSeeded(string? dbName = null)
    {
        var ctx = Create(dbName ?? Guid.NewGuid().ToString());
        SampleData.Seed(ctx);
        return ctx;
    }

    public static SharedTestDbContext CreateInMemorySeeded()
    {
        var ctx = CreateInMemory();
        SampleData.Seed(ctx);
        return ctx;
    }

    public static SharedTestDbContext CreateInMemory(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var ctx = new SharedTestDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static SharedTestDbContext CreateSqlite(string? connectionString = null)
    {
        var connString = string.IsNullOrWhiteSpace(connectionString) 
            ? "Filename=:memory:" 
            : (connectionString.Contains('=') ? connectionString : $"Filename={connectionString}");
        var connection = new SqliteConnection(connString);
        connection.Open();

        var opts = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new SharedTestDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}

// Legacy aliases for backward compatibility with existing tests