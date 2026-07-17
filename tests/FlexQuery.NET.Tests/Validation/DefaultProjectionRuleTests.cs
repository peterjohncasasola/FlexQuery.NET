using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class DefaultProjectionRuleTests
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
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(targetType: null, execOptions: null), result);

        result.IsValid.Should().BeTrue();
        options.Select.Should().BeNull();
    }

    [Fact]
    public void SelectAlreadySet_SkipsInjection()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["Id", "Name"] };
        var options = new QueryOptions { Select = [new SelectModel { Field = "Id" }] };
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }]);
    }

    [Fact]
    public void SelectTreeSet_SkipsInjection()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["Id", "Name"] };
        var options = new QueryOptions { SelectTree = new SelectionNode() };
        options.SelectTree.MarkIncludeAllScalars();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeNullOrEmpty();
    }

    [Fact]
    public void HasProjectionThroughIncludes_SkipsInjection()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["Id", "Name"] };
        var options = new QueryOptions { Includes = ["Children"] };
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeNull();
    }

    [Fact]
    public void InjectsFromSelectableFields()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["Id", "Name"] };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }, new SelectModel { Field = "Name" }]);
    }

    [Fact]
    public void InjectsFromRoleAllowedFields()
    {
        var execOptions = new TestGovernanceOptions
        {
            RoleAllowedFields = new() { ["admin"] = ["Id", "Name", "Age"] },
            CurrentRole = "admin"
        };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }, new SelectModel { Field = "Name" }, new SelectModel { Field = "Age" }]);
    }

    [Fact]
    public void RoleAllowedFields_NoCurrentRole_FallsThrough()
    {
        var execOptions = new TestGovernanceOptions
        {
            RoleAllowedFields = new() { ["admin"] = ["Id", "Name"] },
            AllowedFields = ["Id"]
        };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }]);
    }

    [Fact]
    public void InjectsFromAllowedFields()
    {
        var execOptions = new TestGovernanceOptions { AllowedFields = ["Id", "Name"] };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }, new SelectModel { Field = "Name" }]);
    }

    [Fact]
    public void ExcludesBlockedFields()
    {
        var execOptions = new TestGovernanceOptions { BlockedFields = ["Description"] };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Id" });
        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Name" });
        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Age" });
        options.Select.Should().NotContainEquivalentOf(new SelectModel { Field = "Description" });
    }

    [Fact]
    public void WildcardExpansion_StarOnly()
    {
        var execOptions = new TestGovernanceOptions { SelectableFields = ["*"] };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Id" });
        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Name" });
        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Age" });
        options.Select.Should().ContainEquivalentOf(new SelectModel { Field = "Description" });
    }

    [Fact]
    public void Priority_SelectableFieldsOverAllowedFields()
    {
        var execOptions = new TestGovernanceOptions
        {
            SelectableFields = ["Id"],
            AllowedFields = ["Id", "Name", "Age"]
        };
        var options = new QueryOptions();
        var rule = new DefaultProjectionRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }]);
    }
}


