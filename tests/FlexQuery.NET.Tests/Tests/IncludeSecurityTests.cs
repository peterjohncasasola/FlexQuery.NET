using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Tests;

public class IncludeSecurityTests
{
    [Fact]
    public void AllowedIncludes_NullOrEmpty_AllowsAll()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "Orders", "Profile" },
            Expand = new List<IncludeNode>
            {
                new IncludeNode { Path = "Orders", Children = { new IncludeNode { Path = "Items" } } }
            }
        };

        var exec = new QueryExecutionOptions();
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AllowedIncludes_Configured_AllowsValidIncludes()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "Orders", "Orders.Items", "Profile" },
            Expand = new List<IncludeNode>
            {
                new IncludeNode { Path = "Orders", Children = { new IncludeNode { Path = "Items" } } }
            }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders", "Orders.Items", "Profile" }
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void AllowedIncludes_Configured_RejectsInvalidFlatInclude()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "Orders", "SecretData" }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders" }
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SecretData");
    }

    [Fact]
    public void AllowedIncludes_Configured_RejectsInvalidNestedFilteredInclude()
    {
        var options = new QueryOptions
        {
            Expand = new List<IncludeNode>
            {
                new IncludeNode 
                { 
                    Path = "Orders", 
                    Children = { new IncludeNode { Path = "SecretItems" } } 
                }
            }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders" } // Missing Orders.SecretItems
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Orders.SecretItems");
    }

    [Fact]
    public void AllowedIncludes_Configured_CaseInsensitiveByDefault()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "orders.items" }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders.Items" },
            CaseInsensitiveFields = true
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AllowedIncludes_StrictFieldValidation_ThrowsException()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "SecretData" }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders" },
            StrictFieldValidation = true
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        var ex = Assert.Throws<Exceptions.QueryValidationException>(() => validator.Validate(options, context, result));
        Assert.Contains("SecretData", ex.Message);
    }

    [Fact]
    public void NonStrictValidation_RemovesUnauthorizedIncludes()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "Orders", "SecretData", "Profile" }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string> { "Orders", "Profile" },
            StrictFieldValidation = false
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        // Non-strict mode should record error but remove unauthorized field
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SecretData");
        Assert.Equal(2, options.Includes!.Count);
        Assert.Contains("Orders", options.Includes);
        Assert.Contains("Profile", options.Includes);
        Assert.DoesNotContain("SecretData", options.Includes);
    }

    [Fact]
    public void NonStrictValidation_RemovesUnauthorizedNestedFilteredIncludes()
    {
        var options = new QueryOptions
        {
            Expand = new List<IncludeNode>
            {
                new IncludeNode 
                { 
                    Path = "Orders", 
                    Children = { new IncludeNode { Path = "SecretItems" } } 
                }
            }
        };

        var exec = new QueryExecutionOptions
        {
            AllowedIncludes = ["Orders"], // Missing Orders.SecretItems
            StrictFieldValidation = false
        };
        var context = new QueryContext { ExecutionOptions = exec };
        
        var validator = new IncludeAccessValidator();
        var result = new ValidationResult();

        validator.Validate(options, context, result);

        // Non-strict mode should record error but remove the unauthorized nested include
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Orders.SecretItems");
        Assert.Single(options.Expand!);
        Assert.Empty(options.Expand[0].Children); // SecretItems removed
    }
}
