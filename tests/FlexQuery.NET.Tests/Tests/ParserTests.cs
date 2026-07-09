using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Tests;

    /// <summary>
    /// Tests for ParserUtilities.BuildAggregateAlias.
    /// Parser-specific tests are in Parsers/DslQueryParserTests.cs and Parsers/JqlQueryParserTests.cs.
    /// </summary>
    public class ParserTests
    {
        [Fact]
    public void Having_AggregateAlias_FieldLessCount_ReturnsCount()
    {
        var alias = ParserUtilities.BuildAggregateAlias("count", null);
        alias.Should().Be("Count");
    }

    [Fact]
    public void Having_AggregateAlias_FieldLessSum_ReturnsSum()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", null);
        alias.Should().Be("Sum");
    }

    [Fact]
    public void Having_AggregateAlias_WithField_ReturnsPascalCasePrefixPlusFunction()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", "total");
        alias.Should().Be("TotalSum");
    }

    [Fact]
    public void Having_AggregateAlias_WithMultiWordField_ReturnsPascalCase()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", "grand_total");
        alias.Should().Be("GrandTotalSum");
    }
}
