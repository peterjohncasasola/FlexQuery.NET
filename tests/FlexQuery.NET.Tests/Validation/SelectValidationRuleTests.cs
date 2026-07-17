using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class SelectValidationRuleTests
{
    private static QueryContext Context(Type? targetType = null, QueryGovernanceOptions? execOptions = null) =>
        new() { TargetType = targetType ?? typeof(Order), ExecutionOptions = execOptions };

    private static ValidationResult Validate(QueryOptions options)
    {
        var rule = new SelectValidationRule();
        var result = ValidationResult.Success();
        rule.Validate(options, Context(), result);
        return result;
    }

    [Fact]
    public void NoSelect_Passes()
    {
        var options = new QueryOptions();
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptySelect_Passes()
    {
        var options = new QueryOptions { Select = [] };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidAlias_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name", Alias = "FullName" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidAlias_Nested_Passes()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "Name", Alias = "CustomerName" });

        var options = new QueryOptions { Select = [customer] };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidAlias_StartsWithDigit_FailsWithInvalidAlias()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name", Alias = "1alias" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidAlias);
    }

    [Fact]
    public void InvalidAlias_ContainsHyphen_FailsWithInvalidAlias()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name", Alias = "total-sales" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidAlias);
    }

    [Fact]
    public void InvalidAlias_ContainsSpace_FailsWithInvalidAlias()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name", Alias = "total sales" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidAlias);
    }

    [Fact]
    public void InvalidAlias_Nested_FailsWithInvalidAlias()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "Name", Alias = "1name" });

        var options = new QueryOptions { Select = [customer] };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.InvalidAlias);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("WHERE")]
    [InlineData("GROUP")]
    [InlineData("BY")]
    [InlineData("HAVING")]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData("NOT")]
    [InlineData("IN")]
    [InlineData("LIKE")]
    [InlineData("BETWEEN")]
    [InlineData("IS")]
    [InlineData("CONTAINS")]
    [InlineData("STARTSWITH")]
    [InlineData("ENDSWITH")]
    [InlineData("ANY")]
    [InlineData("ALL")]
    [InlineData("AS")]
    [InlineData("ASC")]
    [InlineData("DESC")]
    [InlineData("NULL")]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    public void ReservedAlias_FailsWithReservedAlias(string reservedAlias)
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name", Alias = reservedAlias }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ReservedAlias);
    }

    [Fact]
    public void ReservedAlias_Nested_FailsWithReservedAlias()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "Name", Alias = "SELECT" });

        var options = new QueryOptions { Select = [customer] };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.ReservedAlias);
    }

    [Fact]
    public void DuplicateAlias_Flat_FailsWithDuplicateAlias()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Name", Alias = "FullName" },
                new SelectNode { Field = "Email", Alias = "FullName" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.DuplicateAlias);
    }

    [Fact]
    public void DuplicateAlias_CaseInsensitive_FailsWithDuplicateAlias()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Name", Alias = "FullName" },
                new SelectNode { Field = "Email", Alias = "fullname" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.DuplicateAlias);
    }

    [Fact]
    public void DuplicateAlias_Nested_FailsWithDuplicateAlias()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "FirstName", Alias = "Name" });
        customer.Children.Add(new SelectNode { Field = "LastName", Alias = "Name" });

        var options = new QueryOptions { Select = [customer] };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.DuplicateAlias);
    }

    [Fact]
    public void SameAlias_DifferentScopes_Passes()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "FirstName", Alias = "Name" });

        var supplier = new SelectNode { Field = "Supplier" };
        supplier.Children.Add(new SelectNode { Field = "FirstName", Alias = "Name" });

        var options = new QueryOptions { Select = [customer, supplier] };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DuplicateWildcard_Flat_FailsWithDuplicateWildcard()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "*" },
                new SelectNode { Field = "*" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.DuplicateWildcard);
    }

    [Fact]
    public void DuplicateWildcard_Nested_FailsWithDuplicateWildcard()
    {
        var customer = new SelectNode { Field = "Customer" };
        customer.Children.Add(new SelectNode { Field = "*" });
        customer.Children.Add(new SelectNode { Field = "*" });

        var options = new QueryOptions { Select = [customer] };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.DuplicateWildcard);
    }

    [Fact]
    public void Wildcard_AndField_SameScope_Passes()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "*" },
                new SelectNode { Field = "Name" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MultipleErrors_Accumulated()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Name", Alias = "SELECT" },
                new SelectNode { Field = "Email", Alias = "SELECT" },
                new SelectNode { Field = "*" },
                new SelectNode { Field = "*" }
            ]
        };
        var result = Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.ReservedAlias);
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.DuplicateAlias);
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.DuplicateWildcard);
    }

    [Fact]
    public void NoAlias_Passes()
    {
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name" }]
        };
        var result = Validate(options);

        result.IsValid.Should().BeTrue();
    }
}
