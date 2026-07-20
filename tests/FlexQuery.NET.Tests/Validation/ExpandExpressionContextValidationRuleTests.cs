using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class ExpandExpressionContextValidationRuleTests
{
    private sealed class TestGovernanceOptions : QueryGovernanceOptions { }

    private static QueryContext Context(Type? targetType = null) =>
        new() { TargetType = targetType ?? typeof(Shared.Models.Customer), ExecutionOptions = new TestGovernanceOptions() };

    [Fact]
    public void Validate_RelativeSort_Valid()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "OrderDate", Descending = true }] }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RootPrefixedSort_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Sort = [new SortNode { Field = "Orders.OrderDate", Descending = true }] }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandRootPrefixedPath);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.OrderDate");
    }

    [Fact]
    public void Validate_RootPrefixedFilter_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Orders.Status", Operator = "eq", Value = "Active" }] } }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandRootPrefixedPath);
    }

    [Fact]
    public void Validate_DeepNested_RelativeToChildEntity_Valid()
    {
        // OrderItems is a child of Orders. Inside OrderItems, "Product" is a property of OrderItem.
        // This test verifies that paths inside orderItems are resolved relative to OrderItem, not Customer.
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems", Sort = [new SortNode { Field = "Id", Descending = false }] }] }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DeepNested_RootPrefixedInChild_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders", Children = [new IncludeNode { Path = "OrderItems", Sort = [new SortNode { Field = "Orders.Product.Name", Descending = false }] }] }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandRootPrefixedPath);
    }

    [Fact]
    public void Validate_MixedFilterAndSort_RootPrefixedInSort_ErrorOnSortOnly()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode
            {
                Path = "Orders",
                Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.Equal, Value = "Active" }] },
                Sort = [new SortNode { Field = "Orders.CreatedDate", Descending = true }]
            }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandRootPrefixedPath);
        result.Errors.Should().ContainSingle(e => e.Field == "Orders.CreatedDate");
    }

    [Fact]
    public void Validate_ComplexFilterWithNestedGroup_RootPrefixedField_Error()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode
            {
                Path = "Orders",
                Filter = new FilterGroup
                {
                    Logic = LogicOperator.And,
                    Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }],
                    Groups = [new FilterGroup { Logic = LogicOperator.Or, Filters = [new FilterCondition { Field = "Orders.Region", Operator = "eq", Value = "US" }] }]
                }
            }]
        };
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(typeof(Shared.Models.Customer)), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ExpandRootPrefixedPath);
    }

    [Fact]
    public void Validate_EmptyExpand_NoErrors()
    {
        var options = new QueryOptions { Expand = [] };
        var rule = new ExpandExpressionContextValidationRule();
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
        var rule = new ExpandExpressionContextValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(null!), result);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
