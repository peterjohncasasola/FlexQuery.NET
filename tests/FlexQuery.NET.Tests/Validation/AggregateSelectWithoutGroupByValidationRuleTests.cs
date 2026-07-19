using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class AggregateSelectWithoutGroupByValidationRuleTests
{
    private static QueryContext Context() => new();

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new AggregateSelectWithoutGroupByValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void SelectWithAggregate_NoGroupBy_Fails()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "CustomerId" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Avg, Field = "CreditLimit", Alias = "AvgCredit" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateSelectWithoutGroupBy);
        result.Errors.Should().OnlyContain(e => e.Field == null);
    }

    [Fact]
    public void SelectWildcardWithAggregate_NoGroupBy_Fails()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "*" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Avg, Field = "CreditLimit", Alias = "AvgCredit" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateSelectWithoutGroupBy);
    }

    [Fact]
    public void MultipleSelectFieldsWithAggregate_NoGroupBy_Fails()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "CustomerId" }, new SelectNode { Field = "CustomerName" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Avg, Field = "CreditLimit", Alias = "AvgCredit" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateSelectWithoutGroupBy);
    }

    [Fact]
    public void AggregateAlone_NoSelect_NoGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Avg, Field = "CreditLimit", Alias = "AvgCredit" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SelectWithAggregate_AndGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "CustomerId" }],
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Avg, Field = "CreditLimit", Alias = "AvgCredit" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SelectWithoutAggregate_NoGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "CustomerId" }, new SelectNode { Field = "*" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}
