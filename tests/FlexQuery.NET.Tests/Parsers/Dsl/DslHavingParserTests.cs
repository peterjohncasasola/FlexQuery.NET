using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Parsers.Dsl;

public class DslHavingParserTests
{
    private static HavingNode? Parse(string? raw) =>
        DslHavingParser.Parse(raw);

    private static QueryOptions ParseDsl(Dictionary<string, string> raw)
    {
        var kvps = raw.ToDictionary(
            kv => kv.Key,
            kv => new StringValues(kv.Value),
            StringComparer.OrdinalIgnoreCase);
        return QueryOptionsParser.Parse(kvps);
    }

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
    [InlineData("sum:field:eq:1", "eq")]
    [InlineData("sum:field:EQ:1", "eq")]
    [InlineData("sum:field:neq:1", "neq")]
    [InlineData("sum:field:NEQ:1", "neq")]
    [InlineData("sum:field:gt:1", "gt")]
    [InlineData("sum:field:gte:1", "gte")]
    [InlineData("sum:field:lt:1", "lt")]
    [InlineData("sum:field:lte:1", "lte")]
    public void Parse_ValidOperator_ReturnsNormalizedOperator(string input, string expectedOperator)
    {
        var result = Parse(input);

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Operator.Should().Be(expectedOperator);
    }

    [Theory]
    [InlineData("sum:total:foobar:100")]
    [InlineData("sum:total:unknownop:100")]
    [InlineData("avg:price:nope:50")]
    [InlineData("count:orders:doesnotexist:1,2,3")]
    [InlineData("sum:total:xyz:100")]
    [InlineData("sum:field:isnull:1")]
    [InlineData("sum:field:isnotnull:1")]
    [InlineData("sum:total:between:10:20")]
    [InlineData("count:orders:in:1,2,3")]
    [InlineData("sum:total:contains:abc")]
    [InlineData("sum:total:like:abc")]
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
            .WithMessage("*Invalid field*");
    }

    [Fact]
    public void Parse_ValidGtOperator_ReturnsCorrectFunctionAndField()
    {
        var result = Parse("sum:total:gt:100");

        result.Should().NotBeNull();
        var having = (HavingConditionNode)result!;
        having.Function.Should().Be(AggregateFunction.Sum);
        having.Field.Should().Be("total");
        having.Operator.Should().Be("gt");
        having.Value.Should().Be("100");
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
        var act = () => Parse("sum:123:gt:0");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Invalid field*");
    }

    [Fact]
    public void DslQuery_ValidAggregateHaving_PassesValidation()
    {
        var options = ParseDsl(new Dictionary<string, string>
        {
            ["aggregate"] = "sum:total",
            ["having"] = "sum:total:gt:100"
        });

        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DslQuery_EmptyAggregates_FailsValidation()
    {
        var options = ParseDsl(new Dictionary<string, string>
        {
            ["having"] = "sum:total:gt:100"
        });

        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void DslQuery_MismatchedFunction_FailsValidation()
    {
        var options = ParseDsl(new Dictionary<string, string>
        {
            ["aggregate"] = "count:id",
            ["having"] = "sum:total:gt:100"
        });

        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void DslQuery_MismatchedField_FailsValidation()
    {
        var options = ParseDsl(new Dictionary<string, string>
        {
            ["aggregate"] = "sum:amount",
            ["having"] = "sum:total:gt:100"
        });

        var rule = new HavingAggregateExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = typeof(Order) }, result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }
}
