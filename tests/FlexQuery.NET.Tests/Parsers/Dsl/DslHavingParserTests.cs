using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Dsl;

public class DslHavingParserTests
{
    private static HavingCondition? Parse(string? raw) =>
        DslHavingParser.Parse(raw);

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
    [InlineData("sum:field:isnull:1", "isnull")]
    [InlineData("sum:field:ISNULL:1", "isnull")]
    [InlineData("sum:field:isnotnull:1", "isnotnull")]
    [InlineData("sum:field:ISNOTNULL:1", "isnotnull")]
    public void Parse_ValidOperator_ReturnsNormalizedOperator(string input, string expectedOperator)
    {
        var result = Parse(input);

        result.Should().NotBeNull();
        result!.Operator.Should().Be(expectedOperator);
    }

    [Theory]
    [InlineData("sum:total:foobar:100")]
    [InlineData("sum:total:unknownop:100")]
    [InlineData("avg:price:nope:50")]
    [InlineData("count:orders:doesnotexist:1,2,3")]
    [InlineData("sum:total:xyz:100")]
    public void Parse_UnsupportedOperator_ThrowsDslParseException(string input)
    {
        var act = () => Parse(input);

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_UnsupportedOperator_ErrorMessageContainsOperator()
    {
        var ex = Record.Exception(() => Parse("sum:total:foobar:100"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("foobar");
        ex.Message.Should().Contain("Unsupported operator");
    }

    [Fact]
    public void Parse_CountStar_ThrowsDslParseException()
    {
        var act = () => Parse("count:*:gt:0");

        act.Should().Throw<DslParseException>()
            .WithMessage("*count:* is not supported*");
    }

    [Fact]
    public void Parse_ValidGtOperator_ReturnsCorrectFunctionAndField()
    {
        var result = Parse("sum:total:gt:100");

        result.Should().NotBeNull();
        result!.Function.Should().Be(AggregateFunction.Sum);
        result.Field.Should().Be("total");
        result.Operator.Should().Be("gt");
        result.Value.Should().Be("100");
    }

    [Fact]
    public void Parse_ValidBetweenOperator_ReturnsCorrectValue()
    {
        var result = Parse("sum:total:between:10:20");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("between");
        result.Value.Should().Be("10:20");
    }

    [Fact]
    public void Parse_ValidInOperator_ReturnsCorrectValue()
    {
        var result = Parse("count:orders:in:1,2,3");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("in");
        result.Value.Should().Be("1,2,3");
    }

    [Fact]
    public void Parse_ValidIsNullOperator_ReturnsCorrectValue()
    {
        var result = Parse("sum:field:isnull:1");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("isnull");
        result.Value.Should().Be("1");
    }

    [Fact]
    public void Parse_ValidIsNotNullOperator_ReturnsCorrectValue()
    {
        var result = Parse("sum:field:isnotnull:1");

        result.Should().NotBeNull();
        result!.Operator.Should().Be("isnotnull");
        result.Value.Should().Be("1");
    }

    [Fact]
    public void Parse_SqlStyleSyntax_ThrowsDslParseException()
    {
        var act = () => Parse("sum(total):gt:100");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Expected format: FUNCTION:Field:OPERATOR:value*");
    }

    [Fact]
    public void Parse_MissingField_ThrowsDslParseException()
    {
        var act = () => Parse("sum::gt:100");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Missing field*");
    }

    [Fact]
    public void Parse_MissingOperator_ThrowsDslParseException()
    {
        var act = () => Parse("sum:total::100");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Unsupported operator*");
    }

    [Fact]
    public void Parse_MissingValue_ThrowsDslParseException()
    {
        var act = () => Parse("sum:total:gt:");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Missing value after operator*");
    }

    [Fact]
    public void Parse_MissingFunction_ThrowsDslParseException()
    {
        var act = () => Parse(":total:gt:100");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Expected format: FUNCTION:Field:OPERATOR:value*");
    }

    [Fact]
    public void Parse_InvalidField_ThrowsDslParseException()
    {
        var act = () => Parse("sum:invalid field:gt:0");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Invalid field*");
    }
}
