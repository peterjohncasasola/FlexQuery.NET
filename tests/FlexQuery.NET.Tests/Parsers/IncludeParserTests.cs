using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Parsers;

public class IncludeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DslParse_Empty_ReturnsEmptyList(string? input)
    {
        var result = DslIncludeParser.Parse(input);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("orders", new[] { "orders" })]
    [InlineData("orders.items", new[] { "orders.items" })]
    [InlineData("orders.items.product", new[] { "orders.items.product" })]
    [InlineData("orders, profile", new[] { "orders", "profile" })]
    [InlineData("orders,profile,address", new[] { "orders", "profile", "address" })]
    [InlineData(" Orders , Profile ", new[] { "Orders", "Profile" })]
    public void DslParse_ValidPaths_ReturnsCorrectPaths(string input, string[] expected)
    {
        var result = DslIncludeParser.Parse(input);
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("orders(")]
    [InlineData("orders[")]
    [InlineData("orders{")]
    [InlineData("orders=")]
    [InlineData("orders:")]
    [InlineData("orders;")]
    [InlineData(".orders")]
    [InlineData("orders.")]
    [InlineData("orders..items")]
    [InlineData("orders(total:gt:100)")]
    [InlineData("orders(status:eq:'active')")]
    [InlineData("orders(sort=-createdAt)")]
    [InlineData("orders(take=10)")]
    [InlineData("orders(select=id,total)")]
    [InlineData("orders(expand=items)")]
    [InlineData("orders[0]")]
    [InlineData(",")]
    [InlineData("orders,")]
    [InlineData(",orders")]
    [InlineData("orders,,profile")]
    public void DslParse_InvalidPath_ThrowsDslParseException(string input)
    {
        var act = () => DslIncludeParser.Parse(input);
        act.Should().Throw<DslParseException>()
            .WithMessage("*navigation property paths*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FqlParse_Empty_ReturnsEmptyList(string? input)
    {
        var result = FqlIncludeParser.Parse(input);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Orders", new[] { "Orders" })]
    [InlineData("Orders.OrderItems", new[] { "Orders.OrderItems" })]
    [InlineData("Orders.OrderItems.Product", new[] { "Orders.OrderItems.Product" })]
    [InlineData("Orders, Profile", new[] { "Orders", "Profile" })]
    [InlineData("Orders,Profile,Address", new[] { "Orders", "Profile", "Address" })]
    [InlineData(" Orders , Profile ", new[] { "Orders", "Profile" })]
    public void FqlParse_ValidPaths_ReturnsCorrectPaths(string input, string[] expected)
    {
        var result = FqlIncludeParser.Parse(input);
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("Orders(")]
    [InlineData("Orders[")]
    [InlineData("Orders{")]
    [InlineData("Orders=")]
    [InlineData("Orders:")]
    [InlineData("Orders;")]
    [InlineData(".Orders")]
    [InlineData("Orders.")]
    [InlineData("Orders..Items")]
    [InlineData("Orders(Total = 100)")]
    [InlineData("Orders(Status = 'Active' AND Amount > 100)")]
    [InlineData("Orders[0]")]
    [InlineData(",")]
    [InlineData("Orders,")]
    [InlineData(",Orders")]
    [InlineData("Orders,,Profile")]
    public void FqlParse_InvalidPath_ThrowsFqlParseException(string input)
    {
        var act = () => FqlIncludeParser.Parse(input);
        act.Should().Throw<FqlParseException>()
            .WithMessage("*navigation property paths*");
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("orders.items")]
    [InlineData("orders,profile,address")]
    public void Parse_DslAndFql_ProduceSameResults(string input)
    {
        var dslResult = DslIncludeParser.Parse(input);
        var fqlResult = FqlIncludeParser.Parse(input);
        dslResult.Should().BeEquivalentTo(fqlResult);
    }
}
