using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class IncludeExpandConsistencyValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Shared.Models.Customer), ExecutionOptions = new TestGovernanceOptions() };

    [Fact]
    public void Validate_IncludeOrders_ExpandOrders_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders" }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeOrdersOrderItems_ExpandOrdersOrderItems_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders", "orders.orderitems"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeOrders_ExpandOrdersOrderItems_Error()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderItems");
    }

    [Fact]
    public void Validate_IncludeOrders_ExpandReviews_Error()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Reviews" }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Reviews");
    }

    [Fact]
    public void Validate_NoIncludes_ExpandWithFilter_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Id", Operator = FilterOperators.Equal, Value = "10001" }] } }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_NoIncludes_ExpandWithSort_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_NoIncludes_ExpandWithSelect_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders" }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_NoIncludes_ExpandWithTake_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Take = 5 }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_NestedMissingInclude_Error()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderItems");
    }

    [Fact]
    public void Validate_IncludeWithExpandFilter_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Id", Operator = FilterOperators.Equal, Value = "10001" }] } }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeWithExpandSort_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeWithExpandTake_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Take = 5 }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeNestedWithNestedExpandFilter_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders", "orders.orderitems"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Price", Operator = FilterOperators.GreaterThan, Value = "100" }] } }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleIncludes_MultipleExpands_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders", "profile"],
            Expand =
            [
                new IncludeNode { Path = "Orders" },
                new IncludeNode { Path = "Profile" }
            ]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleIncludes_OneInvalidExpand_ErrorOnlyOnInvalid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand =
            [
                new IncludeNode { Path = "Orders" },
                new IncludeNode { Path = "Reviews" }
            ]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "Reviews");
        result.Errors.Should().NotContain(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_DeepNestedExpand_PartialInclude_Error()
    {
        var options = new QueryOptions
        {
            Includes = ["orders.orderitems"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems", Children = [new IncludeNode { Path = "Product" }] }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude && e.Field == "Orders");
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude && e.Field == "Orders.OrderItems.Product");
    }

    [Fact]
    public void Validate_EmptyExpand_NoErrors()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = []
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #region Edge Cases

    [Fact]
    public void Validate_IncludeOrders_ExpandOrdersWithOptions_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.Equal, Value = "Active" }] } }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeOrderLinesOnly_ExpandOrdersWithNestedOrderLines_ErrorOnMissingParent()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders.OrderLines"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_IncludeOrdersAndOrderLines_NestedExpand_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders", "Orders.OrderLines"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyInclude_ExpandOrders_Error()
    {
        var options = new QueryOptions
        {
            Includes = [],
            Expand = [new IncludeNode { Path = "Orders" }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_IncludeOrdersOnly_ExpandOrdersWithNestedOrderLines_ErrorOnMissingChild()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderLines");
    }

    [Fact]
    public void Validate_IncludeDeepPath_DeeplyNestedExpand_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders", "orders.orderlines", "orders.orderlines.product"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines", Children = [new IncludeNode { Path = "Product" }] }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeOrders_ExpandOrdersWithSortAndTake_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }], Take = 10 }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeMultiple_ExpandMultipleMixed_Valid()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders", "Profile", "Orders.OrderLines"],
            Expand =
            [
                new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines" }] },
                new IncludeNode { Path = "Profile" }
            ]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IncludeOrders_ExpandOrdersWithMultipleChildren_ErrorOnMissingChild()
    {
        var options = new QueryOptions
        {
            Includes = ["orders"],
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderLines" }, new IncludeNode { Path = "Reviews" }] }]
        };
        var rule = new IncludeExpandConsistencyValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Field == "Orders.OrderLines");
        result.Errors.Should().Contain(e => e.Field == "Orders.Reviews");
    }

    #endregion
}
