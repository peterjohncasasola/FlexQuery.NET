using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class FieldAccessValidationRuleTests
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

    private sealed class DenyAllResolver : IFieldAccessResolver
    {
        public bool IsAllowed(string field, QueryOperation operation, QueryContext context) => false;
    }

    // --- Null / No-op ---

    [Fact]
    public void NullExecOptions_Passes()
    {
        var options = new QueryOptions { Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Id", Operator = "eq", Value = "1" }] } };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: null), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NoSecurityActive_Passes()
    {
        var options = new QueryOptions { Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Id", Operator = "eq", Value = "1" }] } };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: new TestGovernanceOptions()), result);

        result.IsValid.Should().BeTrue();
    }

    // --- Default Sort ---

    [Fact]
    public void DefaultSort_InjectedWhenNoSortSpecified()
    {
        var execOptions = new TestGovernanceOptions { DefaultSortField = "Name" };
        var options = new QueryOptions();
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Sort.Should().ContainSingle(s => s.Field == "Name" && s.Descending == false);
    }

    [Fact]
    public void DefaultSortDescending()
    {
        var execOptions = new TestGovernanceOptions { DefaultSortField = "Name", DefaultSortDescending = true };
        var options = new QueryOptions();
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Sort.Should().ContainSingle(s => s.Field == "Name" && s.Descending);
    }

    [Fact]
    public void DefaultSortNotInjectedWhenSortExists()
    {
        var execOptions = new TestGovernanceOptions { DefaultSortField = "Name" };
        var options = new QueryOptions { Sort = [new SortNode { Field = "Age" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Sort.Should().ContainSingle(s => s.Field == "Age");
    }

    // --- Blocked Fields per Operation ---

    [Fact]
    public void BlockedFieldInFilter_StrictMode_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }] }
        };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
    }

    [Fact]
    public void BlockedFieldInFilter_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }] }
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
        options.Filter.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BlockedFieldInSort_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { Sort = [new SortNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void BlockedFieldInSort_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { Sort = [new SortNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Sort.Should().BeEmpty();
    }

    [Fact]
    public void BlockedFieldInSelect_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void BlockedFieldInSelect_NonStrict_RemovesAndReinjectsDefault()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"],
            SelectableFields = ["Id", "Age"]
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().NotBeEmpty();
        options.Select.Should().NotContainEquivalentOf(new SelectNode { Field = "Name" });
    }

    [Fact]
    public void BlockedFieldInGroupBy_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { GroupBy = ["Name"] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void BlockedFieldInGroupBy_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { GroupBy = ["Name"] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.GroupBy.Should().BeEmpty();
    }

    [Fact]
    public void BlockedFieldInAggregate_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Name", Alias = "cnt" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void BlockedFieldInAggregate_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions { Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Name", Alias = "cnt" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public void BlockedFieldInHaving_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions
        {
            Having = new HavingConditionNode { Function = AggregateFunction.Count, Field = "Name", Operator = "gt", Value = "5" }
        };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    // --- Operation-Level Fields ---

    [Fact]
    public void OperationLevel_FilterableFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            FilterableFields = ["Id"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }] }
        };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void OperationLevel_SortableFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            SortableFields = ["Id"]
        };
        var options = new QueryOptions { Sort = [new SortNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void OperationLevel_SelectableFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            SelectableFields = ["Id"]
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void OperationLevel_GroupableFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            GroupableFields = ["Id"]
        };
        var options = new QueryOptions { GroupBy = ["Name"] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void OperationLevel_AggregatableFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AggregatableFields = ["Id"]
        };
        var options = new QueryOptions { Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Name", Alias = "cnt" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    // --- Global AllowedFields ---

    [Fact]
    public void GlobalAllowedFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedFields = ["Id", "Age"]
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    // --- Depth Validation ---

    [Fact]
    public void DepthValidation_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            MaxFieldDepth = 1
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Children.Label" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void DepthValidation_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            MaxFieldDepth = 1
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Children.Label" }, new SelectNode { Field = "Id" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo(new[] { new SelectNode { Field = "Id" } });
    }

    // --- Custom Resolver ---

    [Fact]
    public void CustomResolver_Denies_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            FieldAccessResolver = new DenyAllResolver()
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void CustomResolver_Denies_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            FieldAccessResolver = new DenyAllResolver()
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEmpty();
    }

    // --- Role-Based ---

    [Fact]
    public void RoleAllowedFields_Strict_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            RoleAllowedFields = new() { ["admin"] = ["Id"] },
            CurrentRole = "admin"
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void RoleAllowedFields_NonStrict_Removes()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            RoleAllowedFields = new() { ["admin"] = ["Id"] },
            CurrentRole = "admin"
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Select.Should().BeEquivalentTo(new[] { new SelectNode { Field = "Id" } });
    }

    // --- Navigation Traversal by Includes ---

    [Fact]
    public void NavigationTraversalByIncludes_AllowsFilteringOnNavProperty()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedFields = ["Id"],
            AllowedIncludes = ["Children"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Children.Label", Operator = "eq", Value = "test" }
                ]
            }
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NavigationTraversalByIncludes_RejectsFieldOutsideAllowed()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedFields = ["Id"],
            AllowedIncludes = ["Children"]
        };
        var options = new QueryOptions { Select = [new SelectNode { Field = "Name" }] };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>();
    }

    // --- SelectTree ---

    [Fact]
    public void SelectTree_BlockedField_RemovesChild()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Name"]
        };
        var selectTree = new SelectionNode();
        selectTree.GetOrAddChild("Id");
        selectTree.GetOrAddChild("Name");
        var options = new QueryOptions { SelectTree = selectTree };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.SelectTree.Should().NotBeNull();
        options.SelectTree.HasChildren.Should().BeTrue();
        options.SelectTree.Children.Should().ContainKey("Id");
        options.SelectTree.Children.Should().NotContainKey("Name");
    }

    [Fact]
    public void SelectTree_IncludeAllScalars_ValidatesEachScalar()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            BlockedFields = ["Description"]
        };
        var selectTree = new SelectionNode();
        selectTree.MarkIncludeAllScalars();
        var options = new QueryOptions { SelectTree = selectTree };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.SelectTree.Should().NotBeNull();
        options.SelectTree.Children.Should().ContainKey("Id");
        options.SelectTree.Children.Should().ContainKey("Name");
        options.SelectTree.Children.Should().ContainKey("Age");
        options.SelectTree.Children.Should().NotContainKey("Description");
    }

    // --- Filter Group Recursion ---

    [Fact]
    public void AllowedFieldFilter_NonStrict_KeepsAllowedFields()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            FilterableFields = ["Id"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Id", Operator = "eq", Value = "1" },
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }
                ]
            }
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Filter.Filters.Should().ContainSingle(f => f.Field == "Id");
    }

    [Fact]
    public void NestedFilterGroup_ValidatesRecursively()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            FilterableFields = ["Id"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Groups =
                [
                    new FilterGroup
                    {
                        Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }]
                    }
                ]
            }
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Filter.Groups.Should().ContainSingle();
        options.Filter.Groups[0].Filters.Should().BeEmpty();
    }

    [Fact]
    public void ScopedFilter_ValidatesRecursively()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = false,
            FilterableFields = ["Children", "Children.Label"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Children",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters =
                            [
                                new FilterCondition { Field = "Label", Operator = "eq", Value = "x" }
                            ]
                        }
                    }
                ]
            }
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        options.Filter.Filters.Should().ContainSingle();
        options.Filter.Filters[0].ScopedFilter.Filters.Should().ContainSingle(f => f.Field == "Label");
    }

    // --- Field Mappings ---

    [Fact]
    public void FieldMappings_NormalizesField()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            FieldMappings = new() { ["alias"] = "Name" },
            BlockedFields = ["Name"]
        };
        var options = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "alias", Operator = "eq", Value = "test" }] }
        };
        var rule = new FieldAccessValidationRule();

        var act = () => rule.Validate(options, Context(execOptions: execOptions), ValidationResult.Success());

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
    }

    // --- Sort with GroupBy (Aggregate Alias Resolution) ---

    [Fact]
    public void SortWithGroupBy_ResolvesAggregateAlias()
    {
        var execOptions = new TestGovernanceOptions
        {
            StrictFieldValidation = true,
            AllowedFields = ["Age"]
        };
        var options = new QueryOptions
        {
            GroupBy = ["Age"],
            Aggregates = [new Aggregate { Function = AggregateFunction.Count, Field = "Age", Alias = "cnt" }],
            Sort = [new SortNode { Field = "cnt" }]
        };
        var rule = new FieldAccessValidationRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(execOptions: execOptions), result);

        result.IsValid.Should().BeTrue();
    }

    // --- Startup Config Validation ---

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_BlockedField_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            DefaultSortField = "Name",
            BlockedFields = ["Name"]
        };

        var act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("DefaultSortField");
    }

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_NotInSortableFields_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            DefaultSortField = "Name",
            SortableFields = ["Id"]
        };

        var act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("DefaultSortField");
    }

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_NotInAllowedFields_Throws()
    {
        var execOptions = new TestGovernanceOptions
        {
            DefaultSortField = "Name",
            AllowedFields = ["Id"]
        };

        var act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("DefaultSortField");
    }

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_Empty_Passes()
    {
        var act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(new TestGovernanceOptions());

        act.Should().NotThrow();
    }
}
