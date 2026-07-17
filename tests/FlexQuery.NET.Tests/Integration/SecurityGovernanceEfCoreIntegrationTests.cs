using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Tests.Integration;

public sealed class SecurityGovernanceEfCoreIntegrationTests : IDisposable
{
    private readonly GovernanceDbContext _db = GovernanceDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // ──────────────────────────────────────────────────────────────
    //  Test 1: AllowedFields restricts default projection
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllowedFields_ShouldRestrictDefaultProjection()
    {
        var options = new QueryOptions { IncludeCount = true };
        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            Read<int>(row, "Id").Should().NotBe(0);
            Read<string>(row, "Name").Should().NotBeNullOrEmpty();
            HasProperty(row, "SSN").Should().BeFalse();
            HasProperty(row, "Salary").Should().BeFalse();
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 2: BlockedFields should exclude fields
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BlockedFields_ShouldExcludeFieldsFromDefaultProjection()
    {
        var options = new QueryOptions { IncludeCount = true };
        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            Read<int>(row, "Id").Should().BeGreaterThan(0);
            Read<string>(row, "Name").Should().NotBeNullOrEmpty();
            Read<decimal>(row, "Salary").Should().BeGreaterThan(0);
            HasProperty(row, "SSN").Should().BeFalse();
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 3: Explicit Select still validated
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExplicitSelect_ShouldThrow_WhenFieldIsBlocked()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode> { new SelectNode { Field = "SSN" } }
        };
        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("SSN");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 4: GroupableFields enforcement
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GroupBy_ShouldThrow_WhenFieldNotInGroupableFields()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Salary" },
            Aggregates = { new AggregateModel { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } },
            Paging = { Disabled = true }
        };
        var execOptions = new EfCoreQueryOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 5: AggregatableFields enforcement
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aggregate_ShouldThrow_WhenFieldNotInAggregatableFields()
    {
        var options = new QueryOptions
        {
            Aggregates = { new AggregateModel { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } },
            Paging = { Disabled = true }
        };
        var execOptions = new EfCoreQueryOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 6: Having enforcement
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Having_ShouldThrow_WhenFieldNotInAggregatableFields()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = { new AggregateModel { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } },
            Having = new HavingCondition { Field = "Salary", Function = AggregateFunction.Sum, Operator = "gt", Value = "0" },
            Paging = { Disabled = true }
        };
        var execOptions = new EfCoreQueryOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 7: DefaultSortField injection
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultSortField_ShouldOrderResults()
    {
        var options = new QueryOptions { Paging = { Page = 1, PageSize = 10 } };
        var execOptions = new EfCoreQueryOptions
        {
            DefaultSortField = "Name",
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);

        var names = result.Data
            .Select(row => Read<string>(row, "Name"))
            .ToList();

        names.Should().BeInAscendingOrder();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 8: NonStrict cleanup
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void NonStrict_ShouldRemoveBlockedFields_AndKeepAllowedFields()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode> { new SelectNode { Field = "Name" }, new SelectNode { Field = "SSN" } }
        };
        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "Name" });
        options.Select.Should().NotContainEquivalentOf(new SelectNode { Field = "SSN" });
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 9: RoleAllowedFields default projection
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoleAllowedFields_ShouldRestrictDefaultProjection()
    {
        var options = new QueryOptions { IncludeCount = true };
        var execOptions = new EfCoreQueryOptions
        {
            CurrentRole = "admin",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            Read<int>(row, "Id").Should().BeGreaterThan(0);
            Read<string>(row, "Name").Should().NotBeNullOrEmpty();
            HasProperty(row, "SSN").Should().BeFalse();
            HasProperty(row, "Salary").Should().BeFalse();
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 10: Wildcard expansion
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllowedFields_Wildcard_ShouldExpandAndProjectNavigationProperties()
    {
        var options = new QueryOptions { IncludeCount = true, Paging = { Page = 1, PageSize = 10 } };
        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Orders.*" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            Read<int>(row, "Id").Should().BeGreaterThan(0);
            Read<string>(row, "Name").Should().NotBeNullOrEmpty();
            HasProperty(row, "SSN").Should().BeFalse();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Phase A — SelectTree Governance Enforcement Tests
    // ══════════════════════════════════════════════════════════════
    //
    //  These tests verify that SelectTree fields are now recursively
    //  validated by FieldAccessValidator.ValidateSelectTree().
    //
    //  In strict mode, violations throw QueryValidationException.
    //  In non-strict mode, violating children are removed from the tree.
    //
    // ══════════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────────────────────
    //  Test A: BlockedFields — simple field via SelectTree
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectTree_Blocks_BlockedField_Strict()
    {
        var options = new QueryOptions { IncludeCount = true };
        options.SelectTree = new SelectionNode();
        options.SelectTree.GetOrAddChild("SSN");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("SSN");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test B: AllowedFields — field not in whitelist via SelectTree
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectTree_Blocks_NotInAllowedFields_Strict()
    {
        var options = new QueryOptions { IncludeCount = true };
        options.SelectTree = new SelectionNode();
        options.SelectTree.GetOrAddChild("SSN");

        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("SSN");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test C: Nested blocked field via SelectTree
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectTree_Blocks_NestedBlockedField_Strict()
    {
        var options = new QueryOptions { IncludeCount = true };
        options.SelectTree = new SelectionNode();
        var orders = options.SelectTree.GetOrAddChild("Orders");
        orders.GetOrAddChild("Total");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.Total" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("Orders.Total");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test D: IncludeAllScalars (wildcard) via SelectTree
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectTree_Blocks_IncludeAllScalarsWithBlocked_Strict()
    {
        var options = new QueryOptions { IncludeCount = true };
        options.SelectTree = new SelectionNode();
        options.SelectTree.MarkIncludeAllScalars();

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("SSN");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test E: CONTROL — flat Select correctly blocks field
    //  (Proves the vulnerability is SelectTree-specific, not a
    //   general governance failure)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FlatSelect_CorrectlyBlocks_BlockedField()
    {
        var options = new QueryOptions { IncludeCount = true };
        // Use flat Select (NOT SelectTree) — the normal code path
        options.Select = new List<SelectNode> { new SelectNode { Field = "SSN" } };

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = true
        };

        // ACT: With flat Select, strict mode should THROW
        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("SSN");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test F: Validation now catches SelectTree blocked fields
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SelectTree_Validation_Blocks_BlockedField_Strict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        options.SelectTree.GetOrAddChild("SSN");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = true
        };

        // FieldAccessValidator now validates SelectTree: should throw
        var act = () => options.Validate(typeof(Customer), execOptions);

        act.Should().Throw<QueryValidationException>()
            .Which.Message.Should().Contain("SSN");
    }


    [Fact]
    public void SelectTree_Removes_BlockedField_NonStrict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        options.SelectTree.GetOrAddChild("SSN");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = ["SSN"],
            StrictFieldValidation = false
        };

        // Non-strict: validation passes, SSN is removed from the tree
        var result = options.Validate(typeof(Customer), execOptions);

        options.SelectTree.Should().BeNull(
            "SelectTree should be nullified when all children are removed in non-strict mode");
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    private static T Read<T>(object row, string propertyName)
    {
        if (row is IReadOnlyDictionary<string, object?> rod
            && rod.TryGetValue(propertyName, out var rov))
            return rov is null ? default! : (T)Convert.ChangeType(rov, typeof(T))!;

        if (row is IDictionary<string, object?> d
            && d.TryGetValue(propertyName, out var dv))
            return dv is null ? default! : (T)Convert.ChangeType(dv, typeof(T))!;

        var prop = row.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null)
            throw new InvalidOperationException($"Property '{propertyName}' not found on {row.GetType().Name}");

        var val = prop.GetValue(row);
        return val is null ? default! : (T)Convert.ChangeType(val, typeof(T))!;
    }

    private static bool HasProperty(object row, string propertyName)
    {
        if (row is IDictionary<string, object?> d)
            return d.ContainsKey(propertyName);
        return row.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null;
    }
}

// ──────────────────────────────────────────────────────────────────
//  Governance-specific DbContext and entities
// ──────────────────────────────────────────────────────────────────

public sealed class GovernanceDbContext : DbContext
{
    private readonly SqliteConnection _connection;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    private GovernanceDbContext(DbContextOptions<GovernanceDbContext> options, SqliteConnection connection)
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
            entity.Ignore(x => x.Address);
            entity.Ignore(x => x.Addresses);
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

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
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

