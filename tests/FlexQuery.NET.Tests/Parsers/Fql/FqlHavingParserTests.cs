using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
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
            .WithMessage("*Expected CloseParen but found Gt*");
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

    [Fact]
    public void Parse_OrCondition_ReturnsLogicalNodeWithOrLogic()
    {
        var result = Parse("COUNT(CustomerGroupId) = 627 OR AVG(CreditLimit) <= 25000");

        result.Should().NotBeNull();
        result.Should().BeOfType<HavingLogicalNode>();
        var logical = (HavingLogicalNode)result!;
        logical.Logic.Should().Be(LogicOperator.Or);
        logical.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_AndCondition_ReturnsLogicalNodeWithAndLogic()
    {
        var result = Parse("COUNT(CustomerGroupId) = 627 AND AVG(CreditLimit) <= 25000");

        result.Should().NotBeNull();
        result.Should().BeOfType<HavingLogicalNode>();
        var logical = (HavingLogicalNode)result!;
        logical.Logic.Should().Be(LogicOperator.And);
        logical.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ParenthesizedOr_ReturnsGroupNode()
    {
        var result = Parse("(COUNT(CustomerGroupId) = 627 OR AVG(CreditLimit) <= 25000)");

        result.Should().NotBeNull();
        result.Should().BeOfType<HavingGroupNode>();
        var group = (HavingGroupNode)result!;
        group.Inner.Should().BeOfType<HavingLogicalNode>();
        var logical = (HavingLogicalNode)group.Inner;
        logical.Logic.Should().Be(LogicOperator.Or);
        logical.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_NestedOrAnd_PreservesPrecedence()
    {
        var result = Parse("(COUNT(CustomerGroupId) = 627 OR AVG(CreditLimit) <= 25000) AND SUM(CreditLimit) > 1000000");

        result.Should().NotBeNull();
        result.Should().BeOfType<HavingLogicalNode>();
        var outer = (HavingLogicalNode)result!;
        outer.Logic.Should().Be(LogicOperator.And);
        outer.Children.Should().HaveCount(2);

        outer.Children[0].Should().BeOfType<HavingGroupNode>();
        var group = (HavingGroupNode)outer.Children[0];
        group.Inner.Should().BeOfType<HavingLogicalNode>();
        var inner = (HavingLogicalNode)group.Inner;
        inner.Logic.Should().Be(LogicOperator.Or);
        inner.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_DoubleParentheses_ReturnsNestedGroupNode()
    {
        var result = Parse("((COUNT(CustomerGroupId) = 627))");

        result.Should().NotBeNull();
        result.Should().BeOfType<HavingGroupNode>();
        var outer = (HavingGroupNode)result!;
        outer.Inner.Should().BeOfType<HavingGroupNode>();
        var inner = (HavingGroupNode)outer.Inner;
        inner.Inner.Should().BeOfType<HavingConditionNode>();
    }

    [Fact]
    public void Parse_OrChildConditions_PreserveOrderAndValues()
    {
        var result = Parse("COUNT(CustomerGroupId) = 627 OR AVG(CreditLimit) <= 25000");

        result.Should().NotBeNull();
        var logical = (HavingLogicalNode)result!;
        logical.Children.Should().HaveCount(2);

        var first = (HavingConditionNode)logical.Children[0];
        first.Function.Should().Be(AggregateFunction.Count);
        first.Field.Should().Be("CustomerGroupId");
        first.Operator.Should().Be("eq");
        first.Value.Should().Be("627");

        var second = (HavingConditionNode)logical.Children[1];
        second.Function.Should().Be(AggregateFunction.Avg);
        second.Field.Should().Be("CreditLimit");
        second.Operator.Should().Be("lte");
        second.Value.Should().Be("25000");
    }
    
}
