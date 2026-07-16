using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class ExpandPathValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Shared.Models.Customer), ExecutionOptions = new TestGovernanceOptions() };

    [Theory]
    [InlineData(typeof(Shared.Models.Customer), "Orders")]
    [InlineData(typeof(Shared.Models.Customer), "Profile")]
    [InlineData(typeof(Shared.Models.Customer), "Address")]
    [InlineData(typeof(Shared.Models.Customer), "Addresses")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.OrderItems")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.OrderItems.Product")]
    public void Validate_NavigationPaths_Passes(Type entityType, string includePath)
    {
        var options = new QueryOptions { Includes = [includePath] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(entityType), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(Shared.Models.Customer), "Name")]
    [InlineData(typeof(Shared.Models.Customer), "Email")]
    [InlineData(typeof(Shared.Models.Customer), "Phone")]
    [InlineData(typeof(Shared.Models.Customer), "Age")]
    [InlineData(typeof(Shared.Models.Customer), "City")]
    [InlineData(typeof(Shared.Models.Customer), "Status")]
    [InlineData(typeof(Shared.Models.Customer), "IsActive")]
    [InlineData(typeof(Shared.Models.Customer), "CreatedAt")]
    [InlineData(typeof(Shared.Models.Customer), "Salary")]
    public void Validate_ScalarRootPath_Fails(Type entityType, string includePath)
    {
        var options = new QueryOptions { Includes = [includePath] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(entityType), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NAVIGATION_PROPERTY_REQUIRED");
        result.Errors.Should().ContainSingle(e => e.Field == includePath);
    }

    [Theory]
    [InlineData(typeof(Shared.Models.Customer), "Orders.Total")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.Price")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.Status")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.OrderItems.Sku")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.OrderItems.Quantity")]
    [InlineData(typeof(Shared.Models.Customer), "Orders.OrderItems.UnitPrice")]
    public void Validate_ScalarMidChain_Fails(Type entityType, string includePath)
    {
        var options = new QueryOptions { Includes = [includePath] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(entityType), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NAVIGATION_PROPERTY_REQUIRED");
        result.Errors.Should().ContainSingle(e => e.Field == includePath);
    }

    [Fact]
    public void Validate_MixedPaths_ReportsOnlyInvalid()
    {
        var options = new QueryOptions { Includes = ["Orders", "Name", "Profile"] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "Name");
        result.Errors.Should().NotContain(e => e.Field == "Orders");
        result.Errors.Should().NotContain(e => e.Field == "Profile");
    }

    [Fact]
    public void Validate_ExpandNode_ScalarPath_Fails()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Name" }]
        };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NAVIGATION_PROPERTY_REQUIRED");
        result.Errors.Should().ContainSingle(e => e.Field == "Name");
    }

    [Fact]
    public void Validate_NullTargetType_SkipsValidation()
    {
        var options = new QueryOptions { Includes = ["Name"] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, new QueryContext { TargetType = null }, result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NonExistentPath_Fails()
    {
        var options = new QueryOptions { Includes = ["NonExistent"] };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.IncludePathNotFound);
    }
}
