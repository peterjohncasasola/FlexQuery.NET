using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Fql;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlHavingParserTests
{
    private static HavingCondition? Parse(string? raw) =>
        FqlHavingParser.Parse(raw);

    [Fact]
    public void Parse_Null_ReturnsNull()
    {
        Parse(null).Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        Parse("").Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNull()
    {
        Parse("   ").Should().BeNull();
    }

    [Theory]
    [InlineData("SUM(Total) > 100", "gt", "100")]
    [InlineData("SUM(Total) >= 100", "gte", "100")]
    [InlineData("SUM(Total) < 100", "lt", "100")]
    [InlineData("SUM(Total) <= 100", "lte", "100")]
    [InlineData("SUM(Total) = 100", "eq", "100")]
    [InlineData("SUM(Total) != 100", "neq", "100")]
    [InlineData("SUM(Total) <> 100", "neq", "100")]
    [InlineData("AVG(Price) LIKE '%abc%'", "like", "%abc%")]
    [InlineData("SUM(Total) IS NULL", "isnull", null)]
    [InlineData("SUM(Total) IS NOT NULL", "isnotnull", null)]
    public void Parse_ValidOperator_ReturnsNormalizedOperator(string input, string expectedOperator, string? expectedValue)
    {
        var result = Parse(input);

        result.Should().NotBeNull();
        result!.Operator.Should().Be(expectedOperator);
        result.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void Parse_ValidBetweenOperator_ReturnsCommaSeparatedValues()
    {
        var result = Parse("SUM(Total) BETWEEN 10 AND 20");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("between");
        result.Value.Should().Be("10,20");
    }

    [Fact]
    public void Parse_ValidInOperator_ReturnsCommaSeparatedValues()
    {
        var result = Parse("COUNT(Orders) IN (1, 2, 3)");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("in");
        result.Value.Should().Be("1,2,3");
    }

    [Fact]
    public void Parse_ValidNotInOperator_ReturnsCommaSeparatedValues()
    {
        var result = Parse("COUNT(Orders) NOT IN (1, 2, 3)");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("notin");
        result.Value.Should().Be("1,2,3");
    }

    [Theory]
    [InlineData("SUM(Total) FOO 100")]
    [InlineData("SUM(Total) BAR 100")]
    [InlineData("SUM(Total) UNKNOWN 100")]
    public void Parse_UnsupportedOperator_ThrowsFqlParseException(string input)
    {
        var act = () => Parse(input);

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_UnsupportedOperator_ErrorMessageContainsOperator()
    {
        var ex = Record.Exception(() => Parse("SUM(Total) FOO 100"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("FOO");
        ex.Message.Should().Contain("Unrecognized operator");
    }

    [Fact]
    public void Parse_Between_MissingAnd_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) BETWEEN 10");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*BETWEEN requires two values*");
    }

    [Fact]
    public void Parse_In_MissingOpeningParen_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) IN 1, 2, 3");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*IN requires a parenthesized list*");
    }

    [Fact]
    public void Parse_In_MissingClosingParen_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) IN (1, 2, 3");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Missing closing parenthesis*");
    }

    [Fact]
    public void Parse_In_EmptyList_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) IN ()");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*IN list cannot be empty*");
    }

    [Fact]
    public void Parse_IsNull_ExtraContent_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) IS NULL EXTRA");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Unexpected content after*");
    }

    [Fact]
    public void Parse_ValidGtOperator_ReturnsCorrectFunctionAndField()
    {
        var result = Parse("SUM(Total) > 100");

        result.Should().NotBeNull();
        result!.Function.Should().Be(AggregateFunction.Sum);
        result.Field.Should().Be("Total");
        result.Operator.Should().Be("gt");
        result.Value.Should().Be("100");
    }

    [Fact]
    public void Parse_ValidBetweenOperator_ReturnsCorrectValue()
    {
        var result = Parse("SUM(Total) BETWEEN 10 AND 20");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("between");
        result.Value.Should().Be("10,20");
    }

    [Fact]
    public void Parse_ValidInOperator_ReturnsCorrectValue()
    {
        var result = Parse("COUNT(Orders) IN (1, 2, 3)");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("in");
        result.Value.Should().Be("1,2,3");
    }

    [Fact]
    public void Parse_ValidIsNullOperator_ReturnsCorrectValue()
    {
        var result = Parse("SUM(Total) IS NULL");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("isnull");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidIsNotNullOperator_ReturnsCorrectValue()
    {
        var result = Parse("SUM(Total) IS NOT NULL");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("isnotnull");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Parse_MissingValue_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total) >");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Missing value after operator*");
    }

    [Fact]
    public void Parse_CountStar_ThrowsFqlParseException()
    {
        var act = () => Parse("COUNT(*) > 0");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*COUNT(*) is not supported*");
    }

    [Fact]
    public void Parse_InvalidField_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Invalid Field) > 0");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Invalid field*");
    }

    [Fact]
    public void Parse_MissingFunctionCall_ThrowsFqlParseException()
    {
        var act = () => Parse("Total > 100");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Missing function call*");
    }

    [Fact]
    public void Parse_MissingClosingParen_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total > 100");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Missing closing parenthesis*");
    }

    [Fact]
    public void Parse_QuotedValues_StrippedCorrectly()
    {
        var result = Parse("SUM(Total) BETWEEN '10' AND '20'");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("between");
        result.Value.Should().Be("10,20");
    }
}
