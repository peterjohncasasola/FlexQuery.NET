using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class IncludeAccessValidationRuleTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
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
    public void NoAllowedIncludes_Passes()
    {
        var options = new QueryOptions { Includes = ["Children"] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: new TestGovernanceOptions()), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AllowedInclude_Passes()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Includes = ["Children"] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DisallowedIncludeStrict_Throws()
    {
        var execOptions = new TestGovernanceOptions { StrictFieldValidation = true, AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Includes = ["NonExistentNav"] };
        var rule = new IncludeAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("NonExistentNav");
    }

    [Fact]
    public void DisallowedIncludeNonStrict_RemovesAndAddsError()
    {
        var execOptions = new TestGovernanceOptions { StrictFieldValidation = false, AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Includes = ["NonExistentNav"] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.IncludeAccessDenied);
        options.Includes.Should().BeEmpty();
    }

    [Fact]
    public void MixedIncludes_RemovesOnlyDisallowed()
    {
        var execOptions = new TestGovernanceOptions { StrictFieldValidation = false, AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Includes = ["Children", "NonExistentNav"] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Includes.Should().BeEquivalentTo(["Children"]);
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.IncludeAccessDenied);
    }

    [Fact]
    public void DisallowedExpandNodeStrict_Throws()
    {
        var execOptions = new TestGovernanceOptions { StrictFieldValidation = true, AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Expand = [new IncludeNode { Path = "NonExistentNav" }] };
        var rule = new IncludeAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void DisallowedExpandNodeNonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions { StrictFieldValidation = false, AllowedIncludes = ["Children"] };
        var options = new QueryOptions { Expand = [new IncludeNode { Path = "NonExistentNav" }] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Expand.Should().BeNullOrEmpty();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.IncludeAccessDenied);
    }

    [Fact]
    public void NestedExpandNode_DisallowedChildRemoved()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = ["Children"]
        };
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Children",
                    Children = [new IncludeNode { Path = "NonExistentChild" }]
                }
            ]
        };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Expand.Should().ContainSingle();
        options.Expand[0].Children.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.IncludeAccessDenied);
    }
}
