using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class GovernanceConfigValidationRuleTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Description { get; set; }
        public List<Child> Children { get; set; } = [];
    }

    private sealed class Child
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null, QueryGovernanceOptions? execOptions = null) =>
        new() { TargetType = targetType ?? typeof(Customer), ExecutionOptions = execOptions };

    [Fact]
    public void NullExecOptions_Passes()
    {
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(targetType: typeof(Customer), execOptions: null), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullTargetType_Passes()
    {
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(targetType: null, execOptions: new TestGovernanceOptions()), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NoGovernanceLists_Passes()
    {
        var execOptions = new TestGovernanceOptions();
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidFieldLists_Passes()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedFields = ["Id", "Name", "Age"],
            BlockedFields = ["Description"],
            SelectableFields = ["Id", "Name"],
            FilterableFields = ["Age"],
            SortableFields = ["Name"],
            GroupableFields = ["Age"],
            AggregatableFields = ["Age"]
        };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidFieldInAllowedFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { AllowedFields = ["Id", "NonExistentField"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "NonExistentField");
    }

    [Fact]
    public void InvalidFieldInBlockedFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { BlockedFields = ["FakeField"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "FakeField");
    }

    [Fact]
    public void InvalidFieldInSelectableFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["BadField"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "BadField");
    }

    [Fact]
    public void InvalidFieldInFilterableFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { FilterableFields = ["Nope"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidFieldInSortableFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { SortableFields = ["Nope"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidFieldInGroupableFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { GroupableFields = ["Nope"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidFieldInAggregatableFields_Fails()
    {
        var execOptions = new TestGovernanceOptions { AggregatableFields = ["Nope"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WildcardFieldEntries_Skipped()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedFields = ["Id", "Name", "Address.*"],
            BlockedFields = ["*Internal*"]
        };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyOrWhitespaceField_Skipped()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedFields = ["Id", ""],
            BlockedFields = [" "]
        };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidInclude_Passes()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Children"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidInclude_Fails()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["NonExistentNav"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound);
    }

    [Fact]
    public void NonNavigationInclude_Fails()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Name"] };
        var rule = new GovernanceConfigValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(new QueryOptions(), Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound);
    }
}
