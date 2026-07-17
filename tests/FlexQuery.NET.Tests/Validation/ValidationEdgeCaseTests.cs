using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class ValidationEdgeCaseTests
{
    private static QueryContext Context(Type? targetType = null, QueryGovernanceOptions? execOptions = null) =>
        new() { TargetType = targetType ?? typeof(Customer), ExecutionOptions = execOptions };

    [Fact]
    public void PaginationModeValidation_RejectsKeysetWithOffset()
    {
        var options = new QueryOptions
        {
            IsKeysetMode = true,
            OffsetExplicitlyRequested = true
        };
        var rule = new PaginationModeValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.PaginationModeConflict);
    }

    [Fact]
    public void PaginationModeValidation_AllowsKeysetWithoutOffset()
    {
        var options = new QueryOptions
        {
            IsKeysetMode = true,
            OffsetExplicitlyRequested = false
        };
        var rule = new PaginationModeValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PaginationModeValidation_AllowsOffsetWithoutKeyset()
    {
        var options = new QueryOptions
        {
            IsKeysetMode = false,
            OffsetExplicitlyRequested = true
        };
        var rule = new PaginationModeValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PaginationModeValidation_AllowsNeither()
    {
        var options = new QueryOptions
        {
            IsKeysetMode = false,
            OffsetExplicitlyRequested = false
        };
        var rule = new PaginationModeValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OperatorValidity_RejectsUnsupportedOperator()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "unsupported_op", Value = "test" }]
            }
        };
        var rule = new OperatorValidityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.InvalidOperator);
    }

    [Fact]
    public void OperatorValidity_RejectsOperatorNotAllowedPerField()
    {
        var execOptions = new TestGovernanceOptions();
        execOptions.AllowOperators("Name", "eq");
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "test" }]
            }
        };
        var rule = new OperatorValidityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.OperatorNotAllowed);
    }

    [Fact]
    public void OperatorValidity_AllowsOperatorAllowedPerField()
    {
        var execOptions = new TestGovernanceOptions();
        execOptions.AllowOperators("Name", "eq", "contains");
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "test" }]
            }
        };
        var rule = new OperatorValidityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OperatorValidity_ValidatesScopedFilterOperators()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Addresses",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Label", Operator = "unsupported_op", Value = "x" }]
                        }
                    }
                ]
            }
        };
        var rule = new OperatorValidityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.InvalidOperator);
    }

    [Fact]
    public void TypeCompatibility_RejectsStringOperatorOnNonStringField()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Id", Operator = "contains", Value = "test" }]
            }
        };
        var rule = new TypeCompatibilityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.TypeMismatch);
    }

    [Fact]
    public void TypeCompatibility_AllowsStringOperatorOnStringField()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "test" }]
            }
        };
        var rule = new TypeCompatibilityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TypeCompatibility_AllowsStartsWithEndsWithLikeOnStringField()
    {
        var rule = new TypeCompatibilityRule();

        foreach (var op in new[] { "startswith", "endswith", "like" })
        {
            var options = new QueryOptions
            {
                Filter = new FilterGroup
                {
                    Filters = [new FilterCondition { Field = "Name", Operator = op, Value = "test" }]
                }
            };
            var result = ValidationResult.Success();
            rule.Validate(options, Context(), result);
            result.IsValid.Should().BeTrue($"operator '{op}' should be valid on string fields");
        }
    }

    [Fact]
    public void TypeCompatibility_RejectsNonConvertibleValue()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Id", Operator = "eq", Value = "not_a_number" }]
            }
        };
        var rule = new TypeCompatibilityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.TypeMismatch);
    }

    [Fact]
    public void TypeCompatibility_AllowsCollectionOperatorsWithoutValueCheck()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Addresses", Operator = "any", Value = null }]
            }
        };
        var rule = new TypeCompatibilityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TypeCompatibility_ReturnsTrueForNullTargetType()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }]
            }
        };
        var rule = new TypeCompatibilityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(targetType: null), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FieldExistence_RejectsNonExistentSortField()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "NonExistentField" }]
        };
        var rule = new FieldExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldNotFound);
    }

    [Fact]
    public void FieldExistence_AllowsValidSortField()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Name" }]
        };
        var rule = new FieldExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FieldExistence_SkipsGroupedAggregateAliasInSort()
    {
        var options = new QueryOptions
        {
            GroupBy = ["City"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Id", Alias = "count" }],
            Sort = [new SortNode { Field = "count" }]
        };
        var rule = new FieldExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FieldExistence_RejectsScopedFilterOnNonCollection()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Name",
                        Operator = "any",
                        Value = null,
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Id", Operator = "eq", Value = "1" }]
                        }
                    }
                ]
            }
        };
        var rule = new FieldExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.NotACollection);
    }

    [Fact]
    public void FieldExistence_ReturnsTrueForNullTargetType()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Name" }]
        };
        var rule = new FieldExistenceRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(targetType: null), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void HavingWithoutGroupBy_AllowsHavingWhenGroupByIsPresent()
    {
        var options = new QueryOptions
        {
            GroupBy = ["City"],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };
        var rule = new HavingWithoutGroupByRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void HavingWithoutGroupBy_AllowsHavingWhenAggregatesArePresent()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };
        var rule = new HavingWithoutGroupByRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void HavingWithoutGroupBy_NullHaving_Passes()
    {
        var options = new QueryOptions { Having = null };
        var rule = new HavingWithoutGroupByRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GroupByIncludeConflict_NullGroupBy_Passes()
    {
        var options = new QueryOptions
        {
            GroupBy = null,
            Includes = ["Children"]
        };
        var rule = new GroupByIncludeConflictRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpandPathValidation_NullTargetType_Passes()
    {
        var options = new QueryOptions
        {
            Includes = ["Addresses"]
        };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(targetType: null), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpandPathValidation_ValidatesNestedIncludePath()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Addresses",
                    Children = [new IncludeNode { Path = "NonExistentChild" }]
                }
            ]
        };
        var rule = new ExpandPathValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.IncludePathNotFound);
    }

    private sealed class TestGovernanceOptions : QueryGovernanceOptions;
}
