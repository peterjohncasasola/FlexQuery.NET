using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;
using FlexQuery.NET.Validation.Rules;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Security;

public class FieldSecurityTests
{
   
    [Fact]
    public void Should_Fail_When_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "SSN:eq:123" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Field_Is_Not_Whitelisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Nested_Field_Is_Blacklisted()
    {
        var filter = new FqlQueryParser().Parse("orders.any(Status = 'Cancelled')");
        var options = new QueryOptions { Filter = filter };
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.Status" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED" && string.Equals(e.Field, "Orders.Status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_Fail_When_Sort_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "SSN:desc" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Select_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id,SSN" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Succeed_When_All_Fields_Are_Allowed()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "Id" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Allow_Wildcards_In_Whitelist()
    {
        var filter = new FqlQueryParser().Parse("Orders.any(Status = 'Cancelled' AND Total > 0)");
        var options = new QueryOptions { Filter = filter };
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Orders", "Orders.*" }
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Block_Field_Via_Custom_Resolver()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var execOptions = new QueryExecutionOptions
        {
            FieldAccessResolver = new MockResolver(allowed: false)
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Field_Depth_Is_Exceeded()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.OrderItems.Id:eq:1" } });
        var execOptions = new QueryExecutionOptions
        {
            MaxFieldDepth = 2
        };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void NonStrictValidation_ShouldRemoveUnauthorizedFilterFields()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john&SSN:eq:123" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };
        
        var result = options.Validate(typeof(Customer), execOptions);

        // Should have error recorded
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        
        // But SSN should be removed from filter
        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(1);
        options.Filter.Filters.First().Field.Should().Be("Name");
    }

    [Fact]
    public void NonStrictValidation_ShouldRemoveUnauthorizedSelectFields()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id,Name,SSN" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };
        
        var result = options.Validate(typeof(Customer), execOptions);

        // Should have error recorded
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        
        // But SSN should be removed from select
        options.Select.Should().NotBeNull();
        options.Select.Should().HaveCount(2);
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
        options.Select.Should().NotContain("SSN");
    }

    [Fact]
    public void NonStrictValidation_ShouldRemoveUnauthorizedSortFields()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "Name:asc,SSN:desc" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };
        
        var result = options.Validate(typeof(Customer), execOptions);

        // Should have error recorded
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        
        // But SSN should be removed from sort
        options.Sort.Should().NotBeNull();
        options.Sort.Should().HaveCount(1);
        options.Sort.First().Field.Should().Be("Name");
    }

    [Fact]
    public void NonStrictValidation_ShouldRemoveUnauthorizedBlockedFields()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john&SSN:eq:123" } });
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };
        
        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        options.Filter!.Filters.Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────────────────────────
    //  GroupBy + GroupableFields regression tests (Scenarios 1-4)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_GroupBy_Field_Not_In_GroupableFields()
    {
        var options = new QueryOptions { GroupBy = new List<string> { "SSN" } };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void NonStrict_ShouldRemove_Unauthorized_GroupBy_Field()
    {
        var options = new QueryOptions { GroupBy = new List<string> { "Name", "SSN" } };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        options.GroupBy.Should().HaveCount(1);
        options.GroupBy![0].Should().Be("Name");
    }

    [Fact]
    public void Should_Succeed_When_GroupBy_Field_Is_In_GroupableFields()
    {
        var options = new QueryOptions { GroupBy = new List<string> { "Name" } };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Succeed_With_Wildcard_GroupableFields()
    {
        var options = new QueryOptions { GroupBy = new List<string> { "Orders.Status" } };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.*" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Aggregate + AggregatableFields regression tests (Scenarios 5-8)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Aggregate_Field_Not_In_AggregatableFields()
    {
        var options = new QueryOptions
        {
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Sum, Field = "SSN", Alias = "total_ssn" }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void NonStrict_ShouldRemove_Unauthorized_Aggregate_Field()
    {
        var options = new QueryOptions
        {
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" },
                new() { Function = AggregateFunction.Sum, Field = "SSN", Alias = "total_ssn" }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        options.Aggregates.Should().HaveCount(1);
    }

    [Fact]
    public void Should_Succeed_When_Aggregate_Field_Is_In_AggregatableFields()
    {
        var options = new QueryOptions
        {
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Succeed_With_Wildcard_AggregatableFields()
    {
        var options = new QueryOptions
        {
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Sum, Field = "Orders.Total", Alias = "total" }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.*" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Having regression tests (Scenarios 9-11)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Having_Field_Not_In_AggregatableFields()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Field = "SSN",
                Operator = "gt",
                Value = "0"
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void NonStrict_ShouldRecordError_When_Having_Field_Unauthorized()
    {
        var options = new QueryOptions
        {
            Having = new HavingCondition
            {
                Function = AggregateFunction.Sum,
                Field = "SSN",
                Operator = "gt",
                Value = "100"
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
    }

    [Fact]
    public void Should_Succeed_When_Having_Field_Is_In_AggregatableFields()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Field = "Id",
                Operator = "gt",
                Value = "0"
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────
    //  DefaultSortField regression tests (Scenarios 12-16)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultSortField_ShouldBeInjected_WhenSortIsEmpty()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "Name"
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Sort.Should().HaveCount(1);
        options.Sort[0].Field.Should().Be("Name");
        options.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void DefaultSortField_ShouldUseDescendingDirection()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "Name",
            DefaultSortDescending = true
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Sort.Should().HaveCount(1);
        options.Sort[0].Field.Should().Be("Name");
        options.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void DefaultSortField_ShouldNotBeInjected_WhenSortAlreadySpecified()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "Id:asc" } });
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "Name"
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Sort.Should().HaveCount(1);
        options.Sort[0].Field.Should().Be("Id");
    }

    [Fact]
    public void DefaultSortField_ShouldNotBeInjected_WhenNull()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = null
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Sort.Should().BeEmpty();
    }

    [Fact]
    public void DefaultSortField_ShouldBeValidated_AgainstSortableFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "SSN",
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Default projection regression tests (Scenarios 17-20)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldUseSelectableFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().HaveCount(2);
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
    }

    [Fact]
    public void DefaultProjection_ShouldUseAllowedFields_WhenNoSelectableFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().HaveCount(1);
        options.Select.Should().Contain("Name");
    }

    [Fact]
    public void DefaultProjection_ShouldExcludeBlockedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
        options.Select.Should().NotContain("SSN");
    }

    [Fact]
    public void DefaultProjection_ShouldNotApply_WhenSelectAlreadySpecified()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id" } });
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "SSN" }
        };

        options.ValidateOrThrow<Customer>(execOptions);

        options.Select.Should().HaveCount(1);
        options.Select.Should().Contain("Id");
        options.Select.Should().NotContain("Name");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Combined governance regression tests (Scenarios 21-22)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Combined_GroupByAggregateHaving_AllAuthorized_ShouldSucceed()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "0" }
        };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        Action act = () => options.ValidateOrThrow<Customer>(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void NonStrict_Combined_ShouldRemoveUnauthorizedAndKeepAuthorized()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name", "SSN" },
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" },
                new() { Function = AggregateFunction.Sum, Field = "SSN", Alias = "total_ssn" }
            },
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "0" }
        };
        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SSN");
        // GroupBy should keep Name, remove SSN
        options.GroupBy.Should().HaveCount(1);
        options.GroupBy![0].Should().Be("Name");
        // Aggregates should remove the SSN aggregate, keep Id
        options.Aggregates.Should().HaveCount(1);
        options.Aggregates[0].Field.Should().Be("Id");
        // Having should pass (Id is allowed)
    }

    // ──────────────────────────────────────────────────────────────────
    //  Startup validation regression tests (Scenarios 23-24)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_ShouldPass_WhenFieldIsAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "Name",
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "Id" }
        };

        Action act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDefaultSortFieldConfiguration_ShouldThrow_WhenFieldIsBlocked()
    {
        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "SSN",
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        Action act = () => FieldAccessValidationRule.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("BlockedFields");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Role-based default projection tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldUseRoleAllowedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            CurrentRole = "admin",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
                ["user"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
            }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().HaveCount(2);
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
    }

    [Fact]
    public void DefaultProjection_ShouldNotUseRoleAllowedFields_WhenRoleHasNoMatch()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            CurrentRole = "nonexistent",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().BeNull();
    }

    [Fact]
    public void DefaultProjection_ShouldFallThroughToBlockedFields_WhenRoleAllowedFieldsNoMatch()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            CurrentRole = "nonexistent",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().NotContain("Name");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Wildcard expansion tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldExpandWildcard_SelectableFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Orders.*" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Orders.Total");
        options.Select.Should().Contain("Orders.Status");
    }

    [Fact]
    public void DefaultProjection_ShouldExpandWildcard_AllowedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Orders.*" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Orders.Total");
        options.Select.Should().Contain("Orders.Status");
    }

    [Fact]
    public void ExpandWildcardFields_ShouldHandleSimpleWildcard()
    {
        var result = DefaultProjectionHelper.ExpandWildcardFields(
            new[] { "Id", "Name", "*" }, typeof(Customer));

        result.Should().Contain("Id");
        result.Should().Contain("Name");
        result.Should().Contain("SSN"); // scalar from *
    }

    // ──────────────────────────────────────────────────────────────────
    //  NonStrict re-apply tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void NonStrict_ShouldReapplyDefaultProjection_WhenAllSelectFieldsBlocked()
    {
        var options = new QueryOptions
        {
            Select = new List<string> { "SSN" }
        };
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse(); // SSN was denied
        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
        options.Select.Should().NotContain("SSN");
    }

    [Fact]
    public void NonStrict_ShouldReapplyDefaultProjection_WhenAllSelectFieldsRemovedByAllowedFields()
    {
        var options = new QueryOptions
        {
            Select = new List<string> { "SSN", "Email" }
        };
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        result.IsValid.Should().BeFalse(); // SSN, Email were denied
        options.Select.Should().NotBeNull();
        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("Name");
        options.Select.Should().NotContain("SSN");
        options.Select.Should().NotContain("Email");
    }

    [Fact]
    public void Strict_ShouldThrow_WhenSelectFieldBlocked_NoReapply()
    {
        var options = new QueryOptions
        {
            Select = new List<string> { "SSN" }
        };
        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = true
        };

        Assert.Throws<QueryValidationException>(() => options.ValidateOrThrow<Customer>(execOptions));
    }

    [Fact]
    public void StrictDefaultProjection_ShouldThrow_WhenInjectedFieldIsBlocked()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Email" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Email" },
            StrictFieldValidation = true
        };

        // DefaultProjectionRule injects ["Id", "Name", "Email"] via SelectableFields
        // FieldAccessValidator then blocks "Email" in strict mode → QueryValidationException
        Assert.Throws<QueryValidationException>(() => options.ValidateOrThrow<Customer>(execOptions));
    }

    [Fact]
    public void StrictDefaultProjection_ShouldThrow_WhenInjectedFieldNotInAllowedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Email" },
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            StrictFieldValidation = true
        };

        // DefaultProjectionRule injects ["Id", "Name", "Email"] via SelectableFields
        // FieldAccessValidator blocks "Email" (not in AllowedFields) in strict mode
        Assert.Throws<QueryValidationException>(() => options.ValidateOrThrow<Customer>(execOptions));
    }

    [Fact]
    public void StrictDefaultProjection_ShouldThrow_WhenAllowedFieldInjectedAndBlocked()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Email" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Email" },
            StrictFieldValidation = true
        };

        // DefaultProjectionRule injects ["Id", "Name", "Email"] via AllowedFields
        // FieldAccessValidator blocks "Email" in strict mode
        Assert.Throws<QueryValidationException>(() => options.ValidateOrThrow<Customer>(execOptions));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Grouped query exclusion tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldNotApply_WhenGroupByProvided()
    {
        var options = new QueryOptions
        {
            GroupBy = new List<string> { "Name" }
        };
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().BeNull();
    }

    [Fact]
    public void DefaultProjection_ShouldNotApply_WhenAggregatesProvided()
    {
        var options = new QueryOptions
        {
            Aggregates = new List<AggregateModel>
            {
                new() { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }
            }
        };
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    //  No governance tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldNotApply_WhenNoGovernance()
    {
        var options = new QueryOptions();

        options.Validate(typeof(Customer));

        options.Select.Should().BeNull();
    }

    [Fact]
    public void DefaultProjection_ShouldNotApply_WhenOnlyFilterableFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            FilterableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Startup configuration validation tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenBlockedAndAllowedIntersect()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "SSN" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("BlockedFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenSelectableNotSubsetOfAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Email" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("SelectableFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenSortableNotSubsetOfAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Salary" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("SortableFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenFilterableNotSubsetOfAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            FilterableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Email" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("FilterableFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenGroupableNotSubsetOfAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "SSN" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("GroupableFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldThrow_WhenAggregatableNotSubsetOfAllowed()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "SSN" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("AggregatableFields");
    }

    [Fact]
    public void ValidateGovernanceConfig_ShouldPass_WhenConfigIsConsistent()
    {
        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            SortableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            FilterableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        Action act = () => GovernanceValidator.ValidateConfiguration(execOptions);

        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Priorities intersection tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_SelectableFieldsTakesPriority_OverAllowedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "Email" },
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            StrictFieldValidation = false
        };

        var result = options.Validate(typeof(Customer), execOptions);

        // DefaultProjectionRule uses SelectableFields first — Email is injected
        // Then FieldAccessValidator checks all Select fields against AllowedFields
        // Email is denied (not in AllowedFields), removed in non-strict mode
        Assert.False(result.IsValid);
        Assert.Contains("Id", options.Select);
        Assert.Contains("Name", options.Select);
        Assert.DoesNotContain("Email", options.Select);
    }

    [Fact]
    public void DefaultProjection_RoleAllowedFieldsTakesPriority_OverAllowedFields()
    {
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            CurrentRole = "admin",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            },
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name", "SSN" }
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().NotBeNull();
        // RoleAllowedFields used for injection: no SSN
        options.Select.Should().HaveCount(2);
        options.Select.Should().NotContain("SSN");
    }

    private class MockResolver : IFieldAccessResolver
    {
        private readonly bool _allowed;
        public MockResolver(bool allowed) => _allowed = allowed;
        public bool IsAllowed(string fieldPath, QueryOperation operation, QueryContext context) => _allowed;
    }
}
