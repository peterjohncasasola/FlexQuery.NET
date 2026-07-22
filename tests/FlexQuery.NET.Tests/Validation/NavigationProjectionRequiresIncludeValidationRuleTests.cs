using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class NavigationProjectionRequiresIncludeValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Customer), ExecutionOptions = new TestGovernanceOptions() };

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new NavigationProjectionRequiresIncludeValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void RootScalarProjection_NoIncludes_Passes()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Id" },
                new SelectNode { Field = "Name" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IncludedNavigationProjection_Passes()
    {
        var orders = new SelectNode { Field = "Orders" };
        orders.Children.Add(new SelectNode { Field = "OrderId" });

        var options = new QueryOptions
        {
            Includes = ["Orders"],
            Select =
            [
                new SelectNode { Field = "Id" },
                orders
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void NestedIncludedNavigationProjection_Passes()
    {
        var orderItems = new SelectNode { Field = "OrderItems" };
        orderItems.Children.Add(new SelectNode { Field = "ProductId" });

        var orders = new SelectNode { Field = "Orders" };
        orders.Children.Add(orderItems);

        var options = new QueryOptions
        {
            Includes = ["Orders", "Orders.OrderItems"],
            Select = [orders]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void NavigationProjectionWithoutInclude_Fails()
    {
        var orders = new SelectNode { Field = "Orders" };
        orders.Children.Add(new SelectNode { Field = "OrderId" });

        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Id" },
                orders
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.NavigationProjectionRequiresInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders");
    }

    [Fact]
    public void NestedNavigationMissingInclude_Fails()
    {
        var orderItems = new SelectNode { Field = "OrderItems" };
        orderItems.Children.Add(new SelectNode { Field = "ProductId" });

        var orders = new SelectNode { Field = "Orders" };
        orders.Children.Add(orderItems);

        var options = new QueryOptions
        {
            Includes = ["Orders"],
            Select = [orders]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.NavigationProjectionRequiresInclude);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderItems");
    }

    [Fact]
    public void NoSelect_Passes()
    {
        var options = new QueryOptions();
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EmptySelect_Passes()
    {
        var options = new QueryOptions { Select = [] };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Wildcard_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "*" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
