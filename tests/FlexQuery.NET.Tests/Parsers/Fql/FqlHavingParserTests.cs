using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlHavingParserTests
{
    private static HavingNode? Parse(string? raw) =>
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
    public void Parse_ValidOperator_ReturnsNormalizedOperator(string input, string expectedOperator, string? expectedValue)
    {
        var result = Parse(input);

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Operator.Should().Be(expectedOperator);
        having.Value.Should().Be(expectedValue);
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

    [Theory]
    [InlineData("SUM(Total) BETWEEN 10 AND 20")]
    [InlineData("COUNT(Orders) IN (1, 2, 3)")]
    [InlineData("COUNT(Orders) NOT IN (1, 2, 3)")]
    public void Parse_UnsupportedHavingOperator_ThrowsFqlParseException(string input)
    {
        var act = () => Parse(input);

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_ValidGtOperator_ReturnsCorrectFunctionAndField()
    {
        var result = Parse("SUM(Total) > 100");

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Function.Should().Be(AggregateFunction.Sum);
        having.Field.Should().Be("Total");
        having.Operator.Should().Be("gt");
        having.Value.Should().Be("100");
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
            .WithMessage("*Expected field name inside aggregate function 'COUNT'*");
    }

    [Fact]
    public void Parse_InvalidField_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(bad-field) > 0");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Invalid field*");
    }

    [Fact]
    public void Parse_MissingFunctionCall_ThrowsFqlParseException()
    {
        var act = () => Parse("Total > 100");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Expected aggregate function*");
    }

    [Fact]
    public void Parse_MissingClosingParen_ThrowsFqlParseException()
    {
        var act = () => Parse("SUM(Total > 100");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Expected CloseParen at position 10, but found Gt*");
    }

    [Fact]
    public void Parse_QuotedValues_StrippedCorrectly()
    {
        var result = Parse("SUM(Total) > '100'");

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Operator.Should().Be("gt");
        having.Value.Should().Be("100");
    }

    [Fact]
    public void Parse_DottedField_ReturnsCorrectField()
    {
        var result = Parse("SUM(Orders.Total) > 100");

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Function.Should().Be(AggregateFunction.Sum);
        having.Field.Should().Be("Orders.Total");
        having.Operator.Should().Be("gt");
        having.Value.Should().Be("100");
    }

    [Fact]
    public void FqlQuery_ValidAggregateHaving_PassesValidation()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "SUM(Total)",
            Having = "SUM(Total) > 100"
        };

        var options = new FqlQueryParser().Parse(parameters);
        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FqlQuery_EmptyAggregates_FailsValidation()
    {
        var parameters = new FlexQueryParameters
        {
            Having = "SUM(Total) > 100"
        };

        var options = new FqlQueryParser().Parse(parameters);
        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void FqlQuery_MismatchedFunction_FailsValidation()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "COUNT(Id)",
            Having = "SUM(Total) > 100"
        };

        var options = new FqlQueryParser().Parse(parameters);
        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void FqlQuery_MismatchedField_FailsValidation()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "SUM(Amount)",
            Having = "SUM(Total) > 100"
        };

        var options = new FqlQueryParser().Parse(parameters);
        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }
    
    
    [Fact]
    public void FqlQuery_HavingOrContainsMissingAggregate_FailsValidation()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "SUM(Total)",
            Having = "SUM(Total) > 100 OR AVG(Cost) < 50"
        };

        var options = new FqlQueryParser().Parse(parameters);
        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
    }
    
}
