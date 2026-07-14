using FlexQuery.NET;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Tests.Tests;

/// <summary>
/// Locks in the governance model changes from the security review:
///   1. Strict-reject is the effective default (secure-by-default).
///   2. A cached per-type safety net reports configured governance fields that do
///      not exist on the entity type.
///   3. AllowedIncludes is the single navigation gate: it additively authorizes
///      navigation traversal in filter/sort/group ("includeable => traversable").
/// </summary>
public class GovernanceReviewChangesTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SSN { get; set; } = string.Empty;
        public CustomerGroup CustomerGroup { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }

    private sealed class CustomerGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private static QueryOptions FilterOn(string field, string op = "eq", string value = "x")
        => new()
        {
            Filter = new FilterGroup
            {
                Filters = { new FilterCondition { Field = field, Operator = op, Value = value } }
            }
        };

    // ── Item 1: strict-reject is the effective default ─────────────────────────

    [Fact]
    public void StrictFieldValidation_DefaultsToTrue()
    {
        new QueryExecutionOptions().StrictFieldValidation.Should().BeTrue();
    }

    [Fact]
    public void UnwiredOptions_BlockedField_Throws_InsteadOfSilentlyDropping()
    {
        var options = FilterOn("SSN");
        // Options never passed through global defaults; must still reject, not silently drop.
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        Action act = () => options.Validate(typeof(Customer), execOptions);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
    }

    [Fact]
    public void LenientMode_RemainsAvailable_AsExplicitOptIn()
    {
        var options = FilterOn("Name", value: "john");
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };
        options.Filter!.Filters.Add(new FilterCondition { Field = "SSN", Operator = "eq", Value = "1" });

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        options.Filter!.Filters.Should().ContainSingle(f => f.Field == "Name");
    }

    // ── Item 3: AllowedIncludes gates navigation traversal (additive) ───────────

    [Fact]
    public void Filter_OnNavigation_Allowed_WhenNavigationInAllowedIncludes()
    {
        var options = FilterOn("CustomerGroup.GroupName", value: "Retail");
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerGroup" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Filter_OnNavigation_Rejected_WhenNavigationNotInAllowedIncludes()
    {
        var options = FilterOn("CustomerGroup.GroupName", value: "Retail");
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
    }

    [Fact]
    public void Sort_OnNavigation_Allowed_WhenNavigationInAllowedIncludes_EvenWithScalarOnlySortableFields()
    {
        var options = new QueryOptions
        {
            Sort = { new SortNode { Field = "CustomerGroup.GroupName" } }
        };
        var execOptions = new QueryExecutionOptions
        {
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerGroup" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Filter_OnCollectionNavigation_Allowed_WhenNavigationInAllowedIncludes()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                {
                    new FilterCondition
                    {
                        Field = "Orders",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = { new FilterCondition { Field = "Total", Operator = "gt", Value = "100" } }
                        }
                    }
                }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void BlockedFields_StillWin_OverAllowedIncludesTraversal()
    {
        var options = FilterOn("CustomerGroup.GroupName", value: "Retail");
        var execOptions = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerGroup" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerGroup.GroupName" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.FieldAccessDenied);
    }

    // ── Item 2: configured governance field-existence safety net ───────────────

    [Fact]
    public void GovernanceConfig_NonexistentAllowedField_ReportsClearError()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Nonexistent" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.Errors.Should().Contain(e =>
            e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "Nonexistent");
    }

    [Fact]
    public void GovernanceConfig_WildcardEntry_IsSkipped()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Orders.*" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.Errors.Should().NotContain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound);
    }

    [Fact]
    public void GovernanceConfig_NonexistentInclude_ReportsClearError()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Nonexistent" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.Errors.Should().Contain(e =>
            e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "Nonexistent");
    }

    [Fact]
    public void GovernanceConfig_ScalarConfiguredAsInclude_ReportsClearError()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.Errors.Should().Contain(e =>
            e.Code == ValidationErrorCodes.GovernanceFieldNotFound && e.Field == "Name");
    }

    [Fact]
    public void GovernanceConfig_ValidConfiguration_ProducesNoGovernanceErrors()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerGroup", "Orders" },
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.Errors.Should().NotContain(e => e.Code == ValidationErrorCodes.GovernanceFieldNotFound);
    }
}
