using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class GroupByProjectionValidationRuleTests
{
    private static QueryContext Context() => new();

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new GroupByProjectionValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void GroupBy_NoSelect_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GroupBy_SelectInGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Select = [new SelectNode { Field = "CustomerId" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GroupBy_SelectNotInGroupBy_Fails()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Select = [new SelectNode { Field = "OrderDate" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.GroupByProjectionMismatch);
    }

    [Fact]
    public void GroupBy_SelectWildcard_Fails()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Select = [new SelectNode { Field = "*" }],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.GroupByWildcardNotAllowed);
    }

    [Fact]
    public void NoGroupBy_AnySelect_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "CustomerId" }, new SelectNode { Field = "OrderDate" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}
