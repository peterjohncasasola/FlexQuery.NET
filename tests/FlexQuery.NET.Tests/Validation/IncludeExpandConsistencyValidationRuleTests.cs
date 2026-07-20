using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
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
            Includes = ["orders.orderitems"],
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
    public void Validate_NoIncludes_AnyExpand_Valid()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders" }]
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
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandPathNotInInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderItems.Product");
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
}
