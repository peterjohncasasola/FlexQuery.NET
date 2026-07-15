using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Builders;

public class FluentFilterBuilderTests
{
    [Fact]
    public void QueryOptionsFilter_BuilderProducesExpectedFilterGroup()
    {
        var options = new QueryOptions()
            .Filter(f => f.Field("Name").Contains("John").And("Age").GreaterThan(18));

        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(2);
        options.Filter.Filters[0].Field.Should().Be("Name");
        options.Filter.Filters[0].Operator.Should().Be("contains");
        options.Filter.Filters[0].Value.Should().Be("John");
        options.Filter.Filters[1].Field.Should().Be("Age");
        options.Filter.Filters[1].Operator.Should().Be("gt");
        options.Filter.Filters[1].Value.Should().Be("18");
    }

    [Fact]
    public void StronglyTypedFilterBuilder_AppliesTypedFilterToQueryable()
    {
        var people = new[] {
            new Person { Name = "John", Age = 20 },
            new Person { Name = "John", Age = 16 },
            new Person { Name = "Alice", Age = 25 }
        }.AsQueryable();

        var result = people.Filter(f => f.Field(x => x.Name).Eq("John").And(x => x.Age).GreaterThan(18)).ToList();

        result.Should().ContainSingle()
            .Which.Name.Should().Be("John");
    }

    [Fact]
    public void FluentFilterBuilder_WithNestedGroups_ProducesCorrectFilterStructure()
    {
        var options = new QueryOptions()
            .Filter(f => f
                .AndGroup(gb =>
                {
                    gb.Field("Age").GreaterThanOrEqual(18);
                    gb.Field("Country").Eq("USA");
                })
                .OrGroup(og =>
                {
                    og.Field("Status").Eq("Premium");
                    og.Field("TotalOrders").GreaterThan(10);
                }));

        options.Filter.Should().NotBeNull();
        options.Filter.Should().NotBeNull();
        options.Filter!.Groups.Should().HaveCount(2);
        
        // First group (AND)
        var andGroup = options.Filter.Groups[0];
        andGroup.Logic.Should().Be(LogicOperator.And);
        andGroup.Filters.Should().HaveCount(2);
        andGroup.Filters[0].Field.Should().Be("Age");
        andGroup.Filters[0].Operator.Should().Be("gte");
        andGroup.Filters[0].Value.Should().Be("18");
        andGroup.Filters[1].Field.Should().Be("Country");
        andGroup.Filters[1].Operator.Should().Be("eq");
        andGroup.Filters[1].Value.Should().Be("USA");
        
        // Second group (OR)
        var orGroup = options.Filter.Groups[1];
        orGroup.Logic.Should().Be(LogicOperator.Or);
        orGroup.Filters.Should().HaveCount(2);
        orGroup.Filters[0].Field.Should().Be("Status");
        orGroup.Filters[0].Operator.Should().Be("eq");
        orGroup.Filters[0].Value.Should().Be("Premium");
        orGroup.Filters[1].Field.Should().Be("TotalOrders");
        orGroup.Filters[1].Operator.Should().Be("gt");
        orGroup.Filters[1].Value.Should().Be("10");
    }

    [Fact]
    public void FluentFilterBuilder_WithVariousOperators_AppliesCorrectlyToQueryable()
    {
        var customers = new[] {
            new Customer { Name = "John Doe", Email = "john@example.com", Age = 30, IsActive = true },
            new Customer { Name = "Jane Smith", Email = "jane@test.com", Age = 25, IsActive = false },
            new Customer { Name = "Bob Johnson", Email = "bob@test.com", Age = 35, IsActive = true }
        }.AsQueryable();

        // Test multiple conditions with different operators
        var result = customers.Filter(f => f
            .Field(c => c.Name).Contains("John")
            .And(c => c.Email).EndsWith("example.com")
            .And(c => c.Age).GreaterThan(25)
            .And(c => c.IsActive).Eq(true))
            .ToList();

        result.Should().ContainSingle()
            .Which.Name.Should().Be("John Doe");
    }

    [Fact]
    public void FluentFilterBuilder_WithInAndBetweenOperators_WorksCorrectly()
    {
        var products = new[] {
            new Product { Name = "Laptop", Price = 999.99m, Category = "Electronics" },
            new Product { Name = "Mouse", Price = 29.99m, Category = "Electronics" },
            new Product { Name = "Book", Price = 14.99m, Category = "Education" },
            new Product { Name = "Desk", Price = 199.99m, Category = "Furniture" }
        }.AsQueryable();

        // Test IN operator
        var result1 = products.Filter(p => p
            .Field(p => p.Category).In("Electronics", "Furniture"))
            .ToList();

        result1.Should().HaveCount(3)
            .And.Contain(p => p.Name == "Laptop")
            .And.Contain(p => p.Name == "Mouse")
            .And.Contain(p => p.Name == "Desk");

        // Test BETWEEN operator
        var result2 = products.Filter(p => p
            .Field(p => p.Price).Between(20m, 100m))
            .ToList();

        result2.Should().ContainSingle()
            .Which.Name.Should().Be("Mouse");
    }

    [Fact]
    public void FluentFilterBuilder_WithNullChecks_WorksCorrectly()
    {
        var users = new[] {
            new User { Name = "John", Email = "john@example.com" },
            new User { Name = "Jane", Email = null },
            new User { Name = "Bob", Email = "bob@test.com" }
        }.AsQueryable();

        // Test IS NULL
        var nullResult = users.Filter(u => u.Field(u => u.Email).IsNull()).ToList();
        nullResult.Should().ContainSingle()
            .Which.Name.Should().Be("Jane");

        // Test IS NOT NULL
        var notNullResult = users.Filter(u => u.Field(u => u.Email).NotNull()).ToList();
        notNullResult.Should().HaveCount(2)
            .And.Contain(u => u.Name == "John")
            .And.Contain(u => u.Name == "Bob");
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class Customer
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class Product
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    private sealed class User
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
