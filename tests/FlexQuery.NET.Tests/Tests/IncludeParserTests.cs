using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Tests;

public class IncludeParserTests
{
    // ─── DSL Include Tests ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DslParse_Empty_ReturnsEmptyList(string? input)
    {
        var result = DslIncludeParser.Parse(input);
        result.Should().BeEmpty();
    }

    [Fact]
    public void DslParse_PlainIncludes_ReturnsUnfilteredNodes()
    {
        var result = DslIncludeParser.Parse("orders, profile");

        result.Should().HaveCount(2);
        
        result[0].Path.Should().Be("orders");
        result[0].Filter.Should().BeNull();
        result[0].Children.Should().BeEmpty();

        result[1].Path.Should().Be("profile");
        result[1].Filter.Should().BeNull();
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void DslParse_NestedIncludes_BuildsHierarchy()
    {
        var result = DslIncludeParser.Parse("orders.orderItems.product");

        result.Should().HaveCount(1);
        
        var level1 = result[0];
        level1.Path.Should().Be("orders");
        level1.Children.Should().HaveCount(1);

        var level2 = level1.Children[0];
        level2.Path.Should().Be("orderItems");
        level2.Children.Should().HaveCount(1);

        var level3 = level2.Children[0];
        level3.Path.Should().Be("product");
        level3.Children.Should().BeEmpty();
    }

    [Fact]
    public void DslParse_FilteredIncludes_ParsesFiltersAtEachLevel()
    {
        var result = DslIncludeParser.Parse("orders(status:eq:Cancelled).orderItems(id:eq:101)");

        result.Should().HaveCount(1);
        
        var orders = result[0];
        orders.Path.Should().Be("orders");
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Filters.Should().HaveCount(1);
        orders.Filter.Filters[0].Field.Should().Be("status");
        orders.Filter.Filters[0].Value.Should().Be("Cancelled");
        
        orders.Children.Should().HaveCount(1);

        var items = orders.Children[0];
        items.Path.Should().Be("orderItems");
        items.Filter.Should().NotBeNull();
        items.Filter!.Filters.Should().HaveCount(1);
        items.Filter.Filters[0].Field.Should().Be("id");
        items.Filter.Filters[0].Value.Should().Be("101");
    }

    [Fact]
    public void DslParse_ComplexFilterWithParentheses_IgnoresInnerParenthesesForSplit()
    {
        var result = DslIncludeParser.Parse("orders(status:eq:Cancelled&(total:gt:100|type:eq:VIP)),profile");

        result.Should().HaveCount(2);
        
        var orders = result[0];
        orders.Path.Should().Be("orders");
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Groups.Should().HaveCount(1);
        
        var profile = result[1];
        profile.Path.Should().Be("profile");
        profile.Filter.Should().BeNull();
    }

    [Theory]
    [InlineData("orders(Status:eq:Cancelled)")]
    [InlineData("orders(Amount:gt:1000)")]
    [InlineData("orders(Status:neq:Cancelled)")]
    [InlineData("orders(Amount:gte:100)")]
    [InlineData("orders(Amount:lt:50)")]
    [InlineData("orders(Amount:lte:200)")]
    [InlineData("orders(Status:eq:Active&Amount:gt:100)")]
    [InlineData("orders(Status:eq:Active|Status:eq:Pending)")]
    public void DslParse_ValidSyntax_DoesNotThrow(string input)
    {
        var act = () => DslIncludeParser.Parse(input);
        act.Should().NotThrow();
    }

    // ─── FQL Include Tests ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FqlParse_Empty_ReturnsEmptyList(string? input)
    {
        var result = FqlIncludeParser.Parse(input);
        result.Should().BeEmpty();
    }

    [Fact]
    public void FqlParse_PlainIncludes_ReturnsUnfilteredNodes()
    {
        var result = FqlIncludeParser.Parse("Orders, Profile");

        result.Should().HaveCount(2);
        result[0].Path.Should().Be("Orders");
        result[0].Filter.Should().BeNull();
        result[1].Path.Should().Be("Profile");
        result[1].Filter.Should().BeNull();
    }

    [Fact]
    public void FqlParse_NestedIncludes_BuildsHierarchy()
    {
        var result = FqlIncludeParser.Parse("Orders.OrderItems.Product");

        result.Should().HaveCount(1);
        result[0].Path.Should().Be("Orders");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Path.Should().Be("OrderItems");
        result[0].Children[0].Children.Should().HaveCount(1);
        result[0].Children[0].Children[0].Path.Should().Be("Product");
    }

    [Fact]
    public void FqlParse_FilteredIncludes_ParsesFiltersAtEachLevel()
    {
        var result = FqlIncludeParser.Parse("Orders(Status = 'Cancelled').OrderItems(Id = 101)");

        result.Should().HaveCount(1);
        
        var orders = result[0];
        orders.Path.Should().Be("Orders");
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Filters.Should().HaveCount(1);
        orders.Filter.Filters[0].Field.Should().Be("Status");
        orders.Filter.Filters[0].Value.Should().Be("Cancelled");
        
        orders.Children.Should().HaveCount(1);
        var items = orders.Children[0];
        items.Path.Should().Be("OrderItems");
        items.Filter.Should().NotBeNull();
        items.Filter!.Filters.Should().HaveCount(1);
        items.Filter.Filters[0].Field.Should().Be("Id");
        items.Filter.Filters[0].Value.Should().Be("101");
    }

    [Fact]
    public void FqlParse_ComplexFilter_UsesFqlGrammar()
    {
        var result = FqlIncludeParser.Parse("Orders(Status = 'Active' AND Amount > 100)");

        result.Should().HaveCount(1);
        var orders = result[0];
        orders.Path.Should().Be("Orders");
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Logic.Should().Be(LogicOperator.And);
        orders.Filter.Filters.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("Orders(Status = 'Cancelled')")]
    [InlineData("Orders(Amount > 1000)")]
    [InlineData("Orders(Status != 'Cancelled')")]
    [InlineData("Orders(Amount >= 100)")]
    [InlineData("Orders(Amount < 50)")]
    [InlineData("Orders(Amount <= 200)")]
    [InlineData("Orders(Status = 'Active' AND Amount > 100)")]
    [InlineData("Orders(Status = 'Active' OR Status = 'Pending')")]
    public void FqlParse_ValidSyntax_DoesNotThrow(string input)
    {
        var act = () => FqlIncludeParser.Parse(input);
        act.Should().NotThrow();
    }
}
