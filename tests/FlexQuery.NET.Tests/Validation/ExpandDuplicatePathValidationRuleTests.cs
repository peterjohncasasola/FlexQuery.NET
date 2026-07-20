using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class ExpandDuplicatePathValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Shared.Models.Customer), ExecutionOptions = new TestGovernanceOptions() };

    [Fact]
    public void Validate_DifferentPaths_NoErrors()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode { Path = "Orders" },
                new IncludeNode { Path = "Reviews" }
            ]
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateRootPaths_Error()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode { Path = "Orders" },
                new IncludeNode { Path = "Orders" }
            ]
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandDuplicatePath);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void Validate_DuplicateChildPaths_Error()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Children =
                    [
                        new IncludeNode { Path = "OrderItems" },
                        new IncludeNode { Path = "OrderItems" }
                    ]
                }
            ]
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandDuplicatePath);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderItems");
    }

    [Fact]
    public void Validate_CaseInsensitiveDuplicate_Error()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode { Path = "Orders" },
                new IncludeNode { Path = "orders" }
            ]
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandDuplicatePath);
    }

    [Fact]
    public void Validate_SameParentDifferentChildPaths_Valid()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Children =
                    [
                        new IncludeNode { Path = "OrderItems" },
                        new IncludeNode { Path = "OrderItems" }
                    ]
                },
                new IncludeNode { Path = "Orders" }
            ]
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Orders");
        result.Errors.Should().Contain(e => e.Field == "Orders.OrderItems");
    }

    [Fact]
    public void Validate_EmptyExpand_NoErrors()
    {
        var options = new QueryOptions
        {
            Expand = []
        };
        var rule = new ExpandDuplicatePathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
