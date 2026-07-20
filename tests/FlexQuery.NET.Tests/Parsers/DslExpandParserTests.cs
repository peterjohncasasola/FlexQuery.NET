using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Tests.Parsers;

public class DslExpandParserTests
{
    #region Basic - Filter Only

    [Fact]
    public void Parse_FilterOnly_SingleExpand_ReturnsExpandAst()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().BeNull();
        result[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Basic - Sort Only

    [Fact]
    public void Parse_SortOnly_SingleExpand_ReturnsExpandAst()
    {
        var result = DslExpandParser.Parse("orders(sort=OrderDate:desc)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Sort![0].Field.Should().Be("OrderDate");
        result[0].Sort![0].Descending.Should().BeTrue();
        result[0].Take.Should().BeNull();
    }

    #endregion

    #region Basic - Take Only

    [Fact]
    public void Parse_TakeOnly_SingleExpand_ReturnsExpandAst()
    {
        var result = DslExpandParser.Parse("orders(take=5)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Basic - Combined Options

    [Fact]
    public void Parse_FilterSortTake_Combined_ReturnsExpandAst()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active; sort=OrderDate:desc; take=5)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Sort![0].Field.Should().Be("OrderDate");
        result[0].Sort![0].Descending.Should().BeTrue();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Multiple Expand Blocks

    [Fact]
    public void Parse_MultipleBlocks_ReturnsMultipleExpandAsts()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active),reviews(filter=rating:gte:4)");

        result.Should().HaveCount(2);
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().NotBeNull();
        result[1].Path[0].Should().Be("reviews");
        result[1].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MultipleBlocks_SamePathTwice_ReturnsTwoNodes()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active),orders(filter=status:eq:Pending))");

        result.Should().HaveCount(2);
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().NotBeNull();
        result[1].Path[0].Should().Be("orders");
        result[1].Filter.Should().NotBeNull();
    }

    #endregion

    #region Nested Children

    [Fact]
    public void Parse_NestedChildren_ReturnsRecursiveTree()
    {
        var result = DslExpandParser.Parse("orders(orderItems(filter=quantity:gt:5))");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MultipleChildrenAtSameLevel_ReturnsMultipleChildren()
    {
        var result = DslExpandParser.Parse("orders(orderItems(filter=quantity:gt:5),reviews(sort=CreatedDate:desc))");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().HaveCount(2);
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Filter.Should().NotBeNull();
        result[0].Children[1].Path[0].Should().Be("reviews");
        result[0].Children[1].Sort.Should().ContainSingle();
    }

    #endregion

    #region Flat Dotted Paths

    [Fact]
    public void Parse_FlatDottedPath_SingleLevel_ReturnsSingleNode()
    {
        var result = DslExpandParser.Parse("orders.orderItems(filter=quantity:gt:5)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_FlatDottedPath_NoOptions_ReturnsEmptyChildren()
    {
        var result = DslExpandParser.Parse("orders.orderItems");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Whitespace Handling

    [Theory]
    [InlineData("orders( filter = status : eq : Active ; sort = OrderDate : desc ; take = 5 )")]
    [InlineData("orders( filter=status:eq:Active; sort=OrderDate:desc; take=5 )")]
    [InlineData("orders(filter=status:eq:Active;sort=OrderDate:desc;take=5)")]
    public void Parse_WhitespaceVariations_ParsesCorrectly(string input)
    {
        var result = DslExpandParser.Parse(input);

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Empty/Missing Values

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = DslExpandParser.Parse("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = DslExpandParser.Parse("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmptyList()
    {
        var result = DslExpandParser.Parse(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyOptionsBlock_ReturnsNodeWithNoOptions()
    {
        var result = DslExpandParser.Parse("orders()");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().BeNull();
    }

    #endregion

    #region Unknown Options

    [Fact]
    public void Parse_UnknownOptionSkip_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(skip=5)");

        act.Should().Throw<DslParseException>()
           .WithMessage("*Unexpected expand option 'skip'*");
    }

    [Fact]
    public void Parse_UnknownOptionSelect_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(select=Name)");

        act.Should().Throw<DslParseException>()
           .WithMessage("*Unexpected expand option 'select'*");
    }

    #endregion

    #region Malformed Syntax

    [Fact]
    public void Parse_MissingEquals_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(filter status:eq:Active)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_EmptyFilterValue_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(filter=)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidTakeValue_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(take=abc)");

        act.Should().Throw<DslParseException>()
           .WithMessage("*Invalid take value*");
    }

    [Fact]
    public void Parse_NegativeTake_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(take=-1)");

        act.Should().Throw<DslParseException>()
           .WithMessage("*Invalid take value*");
    }

    #endregion

    #region Separator Edge Cases

    [Fact]
    public void Parse_DoubleSemicolon_HandlesGracefully()
    {
        Action act = () => DslExpandParser.Parse("orders(filter=status:eq:Active;; sort=OrderDate:desc)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_LeadingSemicolon_ThrowsParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(; filter=status:eq:Active)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_TrailingSemicolon_HandlesGracefully()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active; sort=OrderDate:desc;)");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
    }

    #endregion

    #region Invalid Filter/Sort Delegation

    [Fact]
    public void Parse_InvalidFilterSyntax_ThrowsDslParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(filter=status:)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidSortSyntax_ThrowsDslParseException()
    {
        Action act = () => DslExpandParser.Parse("orders(sort=:)");

        act.Should().Throw<DslParseException>();
    }

    #endregion

    #region Path Variations

    [Fact]
    public void Parse_PathWithDotsButNoOptions_ReturnsEmptyChildren()
    {
        var result = DslExpandParser.Parse("orders.orderItems");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleSegmentPath_NoDot_ReturnsSingleNode()
    {
        var result = DslExpandParser.Parse("orders(filter=status:eq:Active)");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Deep Nesting

    [Fact]
    public void Parse_ThreeLevelNesting_ReturnsDeepTree()
    {
        var result = DslExpandParser.Parse("orders(orderItems(products(filter=price:gt:10)))");

        result.Should().ContainSingle();
        result[0].Path[0].Should().Be("orders");
        result[0].Children[0].Path[0].Should().Be("orderItems");
        result[0].Children[0].Children[0].Path[0].Should().Be("products");
        result[0].Children[0].Children[0].Filter.Should().NotBeNull();
    }

    #endregion

    #region Zero Take Edge Case

    [Fact]
    public void Parse_ZeroTake_ReturnsZero()
    {
        var result = DslExpandParser.Parse("orders(take=0)");

        result.Should().ContainSingle();
        result[0].Take.Should().Be(0);
    }

    #endregion
}
