using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class ExpandSortOnReferenceValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Shared.Models.Customer), ExecutionOptions = new TestGovernanceOptions() };

    [Fact]
    public void Validate_SortOnReferenceNavigation_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Profile", Sort = [new SortNode { Field = "Name", Descending = false }] }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandSortOnReference);
        result.Errors.Should().ContainSingle(e => e.Field == "Profile");
    }

    [Fact]
    public void Validate_TakeOnReferenceNavigation_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Profile", Take = 1 }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandTakeOnReference);
        result.Errors.Should().ContainSingle(e => e.Field == "Profile");
    }

    [Fact]
    public void Validate_SortOnCollectionNavigation_Valid()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }] }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_TakeOnCollectionNavigation_Valid()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Take = 5 }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReferenceNavigation_NoSortOrTake_Valid()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Profile" }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DeepNested_ReferenceNavigationWithSort_Error()
    {
        // Orders is collection, but Customer (nested under Orders) is a reference navigation
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "Customer", Sort = [new SortNode { Field = "Name", Descending = false }] }] }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandSortOnReference);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.Customer");
    }

    [Fact]
    public void Validate_DeepNested_ReferenceNavigationWithTake_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "Customer", Take = 1 }] }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandTakeOnReference);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.Customer");
    }

    [Fact]
    public void Validate_EmptyExpand_NoErrors()
    {
        var options = new QueryOptions { Expand = [] };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullTargetType_NoErrors()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }] }]
        };
        var rule = new ExpandSortOnReferenceValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(null!), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
