using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Parsers;

public class FqlIncludeBlockParserTests
{
    #region Basic - Filter Only

    [Fact]
    public void Parse_FilterOnly_SingleInclude_ReturnsIncludeAst()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active')");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().BeNull();
        result[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Basic - Sort Only

    [Fact]
    public void Parse_SortOnly_SingleInclude_ReturnsIncludeAst()
    {
        var result = FqlIncludeParser.Parse("orders(sort=OrderDate DESC)");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Sort![0].Field.Should().Be("OrderDate");
        result[0].Sort![0].Descending.Should().BeTrue();
        result[0].Take.Should().BeNull();
    }

    #endregion

    #region Basic - Take Only

    [Fact]
    public void Parse_TakeOnly_SingleInclude_ReturnsIncludeAst()
    {
        var result = FqlIncludeParser.Parse("orders(take=5)");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Basic - Combined Options

    [Fact]
    public void Parse_FilterSortTake_Combined_ReturnsIncludeAst()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active'; sort=OrderDate DESC; take=5)");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Sort![0].Field.Should().Be("OrderDate");
        result[0].Sort![0].Descending.Should().BeTrue();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Multiple Expand Blocks

    [Fact]
    public void Parse_MultipleBlocks_ReturnsMultipleIncludeAsts()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active'),reviews(filter=Rating >= 4)");

        result.Should().HaveCount(2);
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().NotBeNull();
        result[1].Path.Should().Contain("reviews");
        result[1].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MultipleBlocks_SamePathTwice_ReturnsTwoNodes()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active'),orders(filter=Status='Pending')");

        result.Should().HaveCount(2);
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().NotBeNull();
        result[1].Path.Should().Contain("orders");
        result[1].Filter.Should().NotBeNull();
    }

    #endregion

    #region Nested Children

    [Fact]
    public void Parse_NestedChildren_ReturnsRecursiveTree()
    {
        var result = FqlIncludeParser.Parse("orders(orderItems(filter=Quantity > 5))");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path.Should().Contain("orderItems");
        result[0].Children[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MultipleChildrenAtSameLevel_ReturnsMultipleChildren()
    {
        var result = FqlIncludeParser.Parse("orders(orderItems(filter=Quantity > 5),reviews(sort=CreatedDate DESC))");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Children.Should().HaveCount(2);
        result[0].Children[0].Path.Should().Contain("orderItems");
        result[0].Children[0].Filter.Should().NotBeNull();
        result[0].Children[1].Path.Should().Contain("reviews");
        result[0].Children[1].Sort.Should().ContainSingle();
    }

    #endregion

    #region Flat Dotted Paths

    [Fact]
    public void Parse_FlatDottedPath_SingleLevel_ReturnsSingleNode()
    {
        var result = FqlIncludeParser.Parse("orders.orderItems(filter=Quantity > 5)");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Path.Should().Contain("orderItems");
        result[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_FlatDottedPath_DeepPath_ReturnsDeepTree()
    {
        var result = FqlIncludeParser.Parse("orders.orderItems.products(filter=Price > 10)");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Path.Should().Contain("orderItems");
        result[0].Path.Should().Contain("products");
        result[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_FlatDottedPath_NoOptions_ReturnsEmptyChildren()
    {
        var result = FqlIncludeParser.Parse("orders.orderItems");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Path.Should().Contain("orderItems");
        result[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Whitespace Handling

    [Theory]
    [InlineData("orders( filter = Status = 'Active' ; sort = OrderDate DESC ; take = 5 )")]
    [InlineData("orders( filter=Status='Active'; sort=OrderDate DESC; take=5 )")]
    [InlineData("orders(filter=Status='Active';sort=OrderDate DESC;take=5)")]
    public void Parse_WhitespaceVariations_ParsesCorrectly(string input)
    {
        var result = FqlIncludeParser.Parse(input);

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Empty/Missing Values

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = FqlIncludeParser.Parse("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = FqlIncludeParser.Parse("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmptyList()
    {
        var result = FqlIncludeParser.Parse(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyOptionsBlock_ReturnsNodeWithNoOptions()
    {
        var result = FqlIncludeParser.Parse("orders()");

        result.Should().ContainSingle();
        result[0].Path.Should().Equal("orders");
        result[0].Filter.Should().BeNull();
        result[0].Sort.Should().BeEmpty();
        result[0].Take.Should().BeNull();
    }

    #endregion

    #region Unknown Options

    [Fact]
    public void Parse_UnknownOptionSkip_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(skip=5)");

        act.Should().Throw<FqlParseException>()
           .WithMessage("*not supported inside include*");
    }

    [Fact]
    public void Parse_UnknownOptionSelect_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(select=Name)");

        act.Should().Throw<FqlParseException>()
           .WithMessage("*Unexpected include option 'select'*");
    }

    #endregion

    #region Malformed Syntax

    [Fact]
    public void Parse_MissingEquals_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(filter Status='Active')");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_MissingCloseParen_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(filter=Status='Active'");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_ExtraCloseParen_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(filter=Status='Active'))");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_EmptyFilterValue_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(filter=)");

        act.Should().Throw<FqlParseException>();
    }

    #endregion

    #region Separator Edge Cases

    [Fact]
    public void Parse_DoubleSemicolon_HandlesGracefully()
    {
        Action act = () => FqlIncludeParser.Parse("orders(filter=Status='Active';; sort=OrderDate DESC)");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_LeadingSemicolon_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(; filter=Status='Active')");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_TrailingSemicolon_HandlesGracefully()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active'; sort=OrderDate DESC;)");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
        result[0].Sort.Should().ContainSingle();
    }

    #endregion

    #region Filter Value with Special Characters

    [Fact]
    public void Parse_FilterWithCommaInString_DoesNotSplit()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Name='Smith, John')");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_FilterWithSemicolonInString_DoesNotSplitOption()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Description='A; B')");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_FilterWithNestedParentheses_DoesNotConfuseDepth()
    {
        var result = FqlIncludeParser.Parse("orders(filter=(Status='Active' or (Priority > 1)))");

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
    }

    #endregion

    #region Sort with Multiple Fields

    [Fact]
    public void Parse_SortMultipleFields_ReturnsMultipleSortNodes()
    {
        var result = FqlIncludeParser.Parse("orders(sort=OrderDate DESC,CreatedDate ASC)");

        result.Should().ContainSingle();
        result[0].Sort.Should().HaveCount(2);
        result[0].Sort![0].Field.Should().Be("OrderDate");
        result[0].Sort![0].Descending.Should().BeTrue();
        result[0].Sort![1].Field.Should().Be("CreatedDate");
        result[0].Sort![1].Descending.Should().BeFalse();
    }

    #endregion

    #region Option Order Independence

    [Fact]
    public void Parse_SortBeforeFilter_SameResult()
    {
        var result1 = FqlIncludeParser.Parse("orders(filter=Status='Active'; sort=OrderDate DESC; take=5)");
        var result2 = FqlIncludeParser.Parse("orders(sort=OrderDate DESC; filter=Status='Active'; take=5)");

        result1.Count.Should().Be(result2.Count);
        result1[0].Filter.Should().NotBeNull();
        result2[0].Filter.Should().NotBeNull();
        result1[0].Sort.Should().HaveCount(1);
        result2[0].Sort.Should().HaveCount(1);
        result1[0].Take.Should().Be(result2[0].Take);
    }

    #endregion

    #region Single Segment Path

    [Fact]
    public void Parse_SingleSegmentPath_NoDot_ReturnsSingleNode()
    {
        var result = FqlIncludeParser.Parse("orders(filter=Status='Active')");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Children.Should().BeEmpty();
    }

    #endregion

    #region Skip Is Not Supported Inside Include

    [Fact]
    public void Parse_SkipOption_ThrowsParseException()
    {
        Action act = () => FqlIncludeParser.Parse("orders(skip=5)");

        act.Should().Throw<FqlParseException>()
           .WithMessage("*not supported inside include*");
    }

    #endregion

    #region Deep Nesting

    [Fact]
    public void Parse_ThreeLevelNesting_ReturnsDeepTree()
    {
        var result = FqlIncludeParser.Parse("orders(orderItems(products(filter=Price > 10)))");

        result.Should().ContainSingle();
        result[0].Path.Should().Contain("orders");
        result[0].Children[0].Path.Should().Contain("orderItems");
        result[0].Children[0].Children[0].Path.Should().Contain("products");
        result[0].Children[0].Children[0].Filter.Should().NotBeNull();
    }

    #endregion
}
