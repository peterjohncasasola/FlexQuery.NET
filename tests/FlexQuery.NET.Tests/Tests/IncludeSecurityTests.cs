using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class IncludeSecurityTests
{
    [Fact]
    public void AllowedIncludes_NullOrEmpty_AllowsAll()
    {
        var options = new QueryOptions
        {
            Includes = new List<string> { "Orders", "Profile" },
            FilteredIncludes = new List<IncludeNode>
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
            FilteredIncludes = new List<IncludeNode>
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
            FilteredIncludes = new List<IncludeNode>
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
}
