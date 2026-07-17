using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class AggregateSortValidationRuleTests
{
    private static QueryContext Context(Type? targetType = null, QueryGovernanceOptions? execOptions = null) =>
        new() { TargetType = targetType ?? typeof(Order), ExecutionOptions = execOptions };

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new AggregateSortValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void EmptySort_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = []
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FieldSort_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "CustomerName", Descending = false }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_MatchingFunctionAndField_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Total", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_CaseInsensitiveField_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "total", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_AliasIgnored_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Total", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_DifferentFunction_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Avg, AggregateField = "Total", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateNotDeclared);
    }

    [Fact]
    public void AggregateSort_DifferentField_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Subtotal", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateNotDeclared);
    }

    [Fact]
    public void AggregateSort_NotDeclared_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Price", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateNotDeclared);
    }

    [Fact]
    public void AggregateSort_NoAggregates_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Total", Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.AggregateNotDeclared);
    }

    [Fact]
    public void AggregateSort_CountCollection_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Orders", Alias = "TotalCount" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_MultipleAggregates_MixedSorts_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates =
            [
                new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" },
                new AggregateModel { Function = AggregateFunction.Avg, Field = "Price", Alias = "AvgPrice" }
            ],
            Sort =
            [
                new SortNode { Field = "CustomerName", Descending = false },
                new SortNode { Field = "Orders", Aggregate = AggregateFunction.Sum, AggregateField = "Total", Descending = true },
                new SortNode { Field = "Orders", Aggregate = AggregateFunction.Avg, AggregateField = "Price", Descending = false }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}