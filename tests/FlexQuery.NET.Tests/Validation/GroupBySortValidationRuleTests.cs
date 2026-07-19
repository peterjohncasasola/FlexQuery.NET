using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class GroupBySortValidationRuleTests
{
    private static QueryContext Context() => new();

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new GroupBySortValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void GroupBy_SortByGroupKey_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "CustomerId", Descending = false }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GroupBy_SortByAggregateAlias_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "TotalSum", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GroupBy_SortByNonGroupedField_Fails()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "OrderDate", Descending = false }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.GroupBySortInvalid);
    }

    [Fact]
    public void NoGroupBy_AnySort_Passes()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "OrderDate", Descending = false }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}
