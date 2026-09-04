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
        public List<GrandChild> Items { get; set; } = [];
    }

    private sealed class GrandChild
    {
        public int Id { get; set; }
        public List<GreatGrandChild> Details { get; set; } = [];
    }

    private sealed class GreatGrandChild
    {
        public int Id { get; set; }
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
    public void AllowedIncludes_Configured_WithNullIncludes_DoesNotMutate()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Children", "Orders"] };
        var options = new QueryOptions { Includes = null };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().BeNull();
    }

    [Fact]
    public void AllowedIncludes_Configured_WithEmptyIncludes_DoesNotAddIncludes()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Children", "Orders"] };
        var options = new QueryOptions { Includes = [] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().BeEmpty();
    }

    [Fact]
    public void AllowedIncludes_Configured_MultipleValidIncludes_AllPass()
    {
        var execOptions = new TestGovernanceOptions { AllowedIncludes = ["Children", "Orders", "Profile"] };
        var options = new QueryOptions { Includes = ["Children", "Orders", "Profile"] };
        var rule = new IncludeAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().HaveCount(3);
        options.Includes.Should().Contain(new[] { "Children", "Orders", "Profile" });
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

    // ──────────────────────────────────────────────────────────────────
    //  Required regression cases: exact-match include access semantics
    // ──────────────────────────────────────────────────────────────────

    private static IncludeAccessValidationRule CreateRule() => new();

    // Case 1 — Explicit parent and child: include=Orders,Orders.OrderItems → both allowed.

    [Fact]
    public void Case1_ExplicitParentAndChild_BothPathsAllowed()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions { Includes = ["Children", "Children.Items"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().BeEquivalentTo(["Children", "Children.Items"]);
    }

    [Fact]
    public void Case1_ExplicitParentAndChild_ExpandBothPathsAllowed()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions
        {
            Includes = ["Children", "Children.Items"],
            Expand = [new IncludeNode { Path = "Children", Children = [new IncludeNode { Path = "Items" }] }]
        };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Expand.Should().ContainSingle();
        options.Expand[0].Children.Should().ContainSingle(c => c.Path == "Items");
    }

    // Case 2 — Parent only: include=Orders → Orders allowed; the deeper path is NOT
    // considered explicitly included (exact-match, no implicit child authorization).

    [Fact]
    public void Case2_ParentOnly_ChildNotExplicitlyIncluded()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions { Includes = ["Children"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().ContainSingle().Which.Should().Be("Children");
        options.Includes.Should().NotContain("Children.Items");
    }

    [Fact]
    public void Case2_ParentOnly_RequestedChild_Strict_ThrowsExactMessage()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions { Includes = ["Children", "Children.Items"] };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Include path 'Children.Items' is not allowed.");
    }

    [Fact]
    public void Case2_ParentOnly_RequestedChild_Lenient_RemovesChildKeepsParent()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions { Includes = ["Children", "Children.Items"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        options.Includes.Should().BeEquivalentTo(["Children"]);
        result.Errors.Should().ContainSingle()
            .Which.Should().Match<ValidationError>(e =>
                e.Code == ValidationErrorCodes.IncludeAccessDenied && e.Field == "Children.Items");
    }

    // Case 3 — Child only: include=Orders.OrderItems → the child is allowed; the parent
    // is NOT automatically considered explicitly included (no implicit parent inclusion).

    [Fact]
    public void Case3_ChildOnly_ChildAllowed_ParentNotImplicitlyIncluded()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children.Items" }
        };
        var options = new QueryOptions { Includes = ["Children.Items"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().ContainSingle().Which.Should().Be("Children.Items");
        options.Includes.Should().NotContain("Children");
    }

    [Fact]
    public void Case3_ChildOnly_ParentRequest_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children.Items" }
        };
        var options = new QueryOptions { Includes = ["Children.Items", "Children"] };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Include path 'Children' is not allowed.");
    }

    // Case 4 — Unrelated path: with include=Orders, an unrelated path such as
    // CustomerGroup continues to follow the existing access rules.

    [Fact]
    public void Case4_UnrelatedPath_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions { Includes = ["Children", "CustomerGroup"] };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Include path 'CustomerGroup' is not allowed.");
    }

    [Fact]
    public void Case4_UnrelatedPath_Lenient_RemovesOnlyUnrelated()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions { Includes = ["Children", "CustomerGroup"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        options.Includes.Should().BeEquivalentTo(["Children"]);
        result.Errors.Should().ContainSingle()
            .Which.Should().Match<ValidationError>(e =>
                e.Code == ValidationErrorCodes.IncludeAccessDenied && e.Field == "CustomerGroup");
    }

    // Case 5 — Exact nested path: include=A,A.B,A.B.C → all three levels are accepted;
    // the validator is not hard-coded for two levels.

    [Fact]
    public void Case5_ThreeLevels_AllDeclared_AllAllowed()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Children", "Children.Items", "Children.Items.Details" }
        };
        var options = new QueryOptions { Includes = ["Children", "Children.Items", "Children.Items.Details"] };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Includes.Should().BeEquivalentTo(["Children", "Children.Items", "Children.Items.Details"]);
    }

    [Fact]
    public void Case5_ThreeLevels_AllDeclared_ExpandTreeAllowed()
    {
        var execOptions = new TestGovernanceOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Children", "Children.Items", "Children.Items.Details" }
        };
        var options = new QueryOptions
        {
            Includes = ["Children", "Children.Items", "Children.Items.Details"],
            Expand =
            [
                new IncludeNode
                {
                    Path = "Children",
                    Children =
                    [
                        new IncludeNode { Path = "Items", Children = [new IncludeNode { Path = "Details" }] }
                    ]
                }
            ]
        };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
        options.Expand[0].Children[0].Children.Should().ContainSingle(c => c.Path == "Details");
    }

    [Fact]
    public void Case5_ThreeLevels_PartiallyDeclared_DeepPath_Strict_ThrowsExactPath()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions { Includes = ["Children", "Children.Items", "Children.Items.Details"] };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Include path 'Children.Items.Details' is not allowed.");
    }

    // Expand access granularity: an unauthorized descendant must not deny its authorized
    // parent or siblings; lenient mode prunes only the unauthorized node; strict mode
    // names the exact unauthorized path.

    [Fact]
    public void Expand_Lenient_UnauthorizedChild_KeepsAuthorizedParentAndSibling()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Children",
                    Children = [new IncludeNode { Path = "Items" }, new IncludeNode { Path = "Extra" }]
                }
            ]
        };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.Errors.Should().ContainSingle()
            .Which.Should().Match<ValidationError>(e =>
                e.Code == ValidationErrorCodes.IncludeAccessDenied && e.Field == "Children.Extra");
        options.Expand.Should().ContainSingle().Which.Path.Should().Be("Children");
        options.Expand[0].Children.Should().ContainSingle(c => c.Path == "Items");
    }

    [Fact]
    public void Expand_Strict_UnauthorizedChild_ThrowsExactPath()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Children",
                    Children = [new IncludeNode { Path = "Items" }, new IncludeNode { Path = "Extra" }]
                }
            ]
        };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Expand path 'Children.Extra' is not allowed.");
    }

    [Fact]
    public void Expand_Lenient_DeepUnauthorizedDescendant_PrunesOnlyDeepNode()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children", "Children.Items" }
        };
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Children",
                    Children =
                    [
                        new IncludeNode { Path = "Items", Children = [new IncludeNode { Path = "Details" }] }
                    ]
                }
            ]
        };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        result.Errors.Should().ContainSingle()
            .Which.Field.Should().Be("Children.Items.Details");
        var root = options.Expand.Should().ContainSingle().Subject;
        root.Path.Should().Be("Children");
        var items = root.Children.Should().ContainSingle().Subject;
        items.Path.Should().Be("Items");
        items.Children.Should().BeEmpty();
    }

    [Fact]
    public void Expand_UnauthorizedRoot_Strict_ThrowsRootPath()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Profile" }]
        };

        var act = () => CreateRule().Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
            .WithMessage("Expand path 'Profile' is not allowed.");
    }

    [Fact]
    public void Expand_Lenient_UnauthorizedRoot_RemovedAuthorizedRootKept()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Children" }
        };
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Profile" }, new IncludeNode { Path = "Children" }]
        };
        var result = ValidationResult.Success();

        CreateRule().Validate(options, Context(execOptions: execOptions), result);

        options.Expand.Should().ContainSingle().Which.Path.Should().Be("Children");
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("Profile");
    }
}
