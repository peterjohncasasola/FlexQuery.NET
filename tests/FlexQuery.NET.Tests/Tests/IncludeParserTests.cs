using FlexQuery.NET.Parsers;
using FluentAssertions;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class IncludeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_Empty_ReturnsEmptyList(string? input)
    {
        var result = FilteredIncludeParser.Parse(input);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PlainIncludes_ReturnsUnfilteredNodes()
    {
        var result = FilteredIncludeParser.Parse("orders, profile");

        result.Should().HaveCount(2);
        
        result[0].Path.Should().Be("orders");
        result[0].Filter.Should().BeNull();
        result[0].Children.Should().BeEmpty();

        result[1].Path.Should().Be("profile");
        result[1].Filter.Should().BeNull();
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NestedIncludes_BuildsHierarchy()
    {
        var result = FilteredIncludeParser.Parse("orders.orderItems.product");

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
    public void Parse_FilteredIncludes_ParsesFiltersAtEachLevel()
    {
        var result = FilteredIncludeParser.Parse("orders(status = 'Cancelled').orderItems(id = 101)");

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
    public void Parse_ComplexFilterWithParentheses_IgnoresInnerParenthesesForSplit()
    {
        var result = FilteredIncludeParser.Parse("orders(status = 'Cancelled' AND (total > 100 OR type = 'VIP')),profile");

        result.Should().HaveCount(2);
        
        var orders = result[0];
        orders.Path.Should().Be("orders");
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Groups.Should().HaveCount(1); // The nested OR group
        
        var profile = result[1];
        profile.Path.Should().Be("profile");
        profile.Filter.Should().BeNull();
    }
}
