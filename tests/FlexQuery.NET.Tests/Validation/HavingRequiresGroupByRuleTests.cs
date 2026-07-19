using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class HavingRequiresGroupByRuleTests
{
    private static QueryContext Context() => new();

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new HavingRequiresGroupByRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void HavingWithoutGroupBy_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Id", Alias = "idCount" }],
            Having = new HavingConditionNode { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingRequiresGroupBy);
    }

    [Fact]
    public void HavingWithGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Id", Alias = "idCount" }],
            Having = new HavingConditionNode { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NoHaving_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}
