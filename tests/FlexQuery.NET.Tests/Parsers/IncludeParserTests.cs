using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Parsers;

/// <summary>
/// Grammar tests for the unified <c>include</c> query option: relationship inclusion
/// (simple or with nested query blocks) — the parser checks syntax only; relationship
/// existence/cardinality validation is the semantic validator's job.
/// </summary>
public class IncludeParserTests
{
    private static string[] ParseDslPaths(string? input)
        => IncludeTestFactory.PathStrings(IncludeNormalizer.Normalize(DslIncludeParser.Parse(input)));

    private static string[] ParseFqlPaths(string? input)
        => IncludeTestFactory.PathStrings(IncludeNormalizer.Normalize(FqlIncludeParser.Parse(input)));

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
    [InlineData("orders.items", new[] { "orders", "orders.items" })]
    [InlineData("orders.items.product", new[] { "orders", "orders.items", "orders.items.product" })]
    [InlineData("orders, profile", new[] { "orders", "profile" })]
    [InlineData("orders,profile,address", new[] { "orders", "profile", "address" })]
    [InlineData(" Orders , Profile ", new[] { "Orders", "Profile" })]
    public void DslParse_ValidPaths_ReturnsCorrectPaths(string input, string[] expected)
    {
        ParseDslPaths(input).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("orders(take=5)")]
    [InlineData("orders(filter=status:eq:active)")]
    [InlineData("orders(sort=orderdate:desc)")]
    [InlineData("orders(take=5;filter=status:eq:active;sort=orderdate:desc)")]
    [InlineData("address(take=5)")] // single-valued target is a semantic error, not a syntax error
    public void DslParse_ValidOptions_ReturnsCorrectPaths(string input)
    {
        var result = DslIncludeParser.Parse(input);
        result.Should().ContainSingle();
        result[0].Path[0].Should().NotBeNullOrWhiteSpace();
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
    [InlineData(",")]
    [InlineData("orders,")]
    [InlineData(",orders")]
    [InlineData("orders,,profile")]
    [InlineData("(take=5)")]
    public void DslParse_MalformedSyntax_ThrowsDslParseException(string input)
    {
        var act = () => DslIncludeParser.Parse(input);
        act.Should().Throw<DslParseException>();
    }

    [Theory]
    [InlineData("orders(total:gt:100)")]           // a value without an option key
    [InlineData("orders(select=id,total)")]        // select is not supported inside include
    [InlineData("orders(expand=items)")]           // the removed expand keyword
    [InlineData("orders(status:eq:'active')")]     // no key before the filter expression
    public void DslParse_InvalidOptions_ThrowsDslParseException(string input)
    {
        var act = () => DslIncludeParser.Parse(input);
        act.Should().Throw<DslParseException>();
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
    [InlineData("Orders.OrderItems", new[] { "Orders", "Orders.OrderItems" })]
    [InlineData("Orders.OrderItems.Product", new[] { "Orders", "Orders.OrderItems", "Orders.OrderItems.Product" })]
    [InlineData("Orders, Profile", new[] { "Orders", "Profile" })]
    [InlineData("Orders,Profile,Address", new[] { "Orders", "Profile", "Address" })]
    [InlineData(" Orders , Profile ", new[] { "Orders", "Profile" })]
    public void FqlParse_ValidPaths_ReturnsCorrectPaths(string input, string[] expected)
    {
        ParseFqlPaths(input).Should().BeEquivalentTo(expected);
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
    [InlineData(",")]
    [InlineData("Orders,")]
    [InlineData(",Orders")]
    [InlineData("Orders,,Profile")]
    [InlineData("Orders(filter=Status = 'Active') AND")]
    public void FqlParse_MalformedSyntax_ThrowsFqlParseException(string input)
    {
        var act = () => FqlIncludeParser.Parse(input);
        act.Should().Throw<FqlParseException>();
    }

    [Theory]
    [InlineData("Orders(Total = 100)")]
    [InlineData("Orders(Status = 'Active' AND Amount > 100)")]
    public void FqlParse_InvalidOptions_ThrowsFqlParseException(string input)
    {
        var act = () => FqlIncludeParser.Parse(input);
        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void FqlParse_FilterBlock_KeepsFqlExpressionSyntax()
    {
        // FQL filter syntax stays valid inside an include block (spec: DSL and FQL are
        // not mixed).
        var result = FqlIncludeParser.Parse("orders(filter=status = 'active';sort=orderdate desc;take=5)");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle().Which.Descending.Should().BeTrue();
        result[0].Take.Should().Be(5);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("orders.items")]
    [InlineData("orders,profile,address")]
    public void Parse_DslAndFql_ProduceSamePathsForSimpleIncludes(string input)
    {
        ParseDslPaths(input).Should().BeEquivalentTo(ParseFqlPaths(input));
    }
}
