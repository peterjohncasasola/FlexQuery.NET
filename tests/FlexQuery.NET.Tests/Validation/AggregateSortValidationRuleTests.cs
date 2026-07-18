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
        new() { TargetType = targetType, ExecutionOptions = execOptions };

    private static ValidationResult Validate(QueryOptions options, Type? targetType = null)
    {
        var rule = new AggregateSortValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(targetType), result);
        return result;
    }

    [Fact]
    public void EmptySort_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }],
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
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Orders", Alias = "TotalCount" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options, typeof(Customer));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_MultipleAggregates_MixedSorts_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates =
            [
                new Aggregate { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" },
                new Aggregate { Function = AggregateFunction.Avg, Field = "Price", Alias = "AvgPrice" }
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

    [Fact]
    public void AggregateSort_Count_CollectionTarget_Valid()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Orders", Alias = "TotalCount" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options, typeof(Customer));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_Count_AlternateCollectionTarget_Valid()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Addresses", Alias = "AddressCount" }],
            Sort = [new SortNode { Field = "Addresses", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = false }]
        };
        var result = Validate(options, typeof(Customer));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AggregateSort_Count_ScalarTarget_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Orders.Total", Alias = "InvalidCount" }],
            Sort = [new SortNode { Field = "Orders.Total", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options, typeof(Customer));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidCountTarget);
    }

    [Fact]
    public void AggregateSort_Count_ScalarNavigationTarget_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Profile", Alias = "InvalidCount" }],
            Sort = [new SortNode { Field = "Profile", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options, typeof(Customer));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidCountTarget);
    }

    [Fact]
    public void AggregateSort_Count_NoTargetType_SkipsValidation()
    {
        var options = new QueryOptions
        {
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Orders", Alias = "TotalCount" }],
            Sort = [new SortNode { Field = "Orders", Aggregate = AggregateFunction.Count, AggregateField = null, Descending = true }]
        };
        var result = Validate(options, targetType: null);

        result.IsValid.Should().BeTrue();
    }
}