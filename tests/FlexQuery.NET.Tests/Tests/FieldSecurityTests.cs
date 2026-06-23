using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Jql;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Security;
using FlexQuery.NET.Validation.Rules;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class FieldSecurityTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SSN { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

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
        var filter = new JqlQueryParser().Parse("orders.any(Status = 'Cancelled')");
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
        var filter = new JqlQueryParser().Parse("Orders.any(Status = 'Cancelled' AND Total > 0)");
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
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.Items.Id:eq:1" } });
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
                new() { Function = "sum", Field = "SSN", Alias = "total_ssn" }
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
                new() { Function = "count", Field = "Id", Alias = "cnt" },
                new() { Function = "sum", Field = "SSN", Alias = "total_ssn" }
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
                new() { Function = "count", Field = "Id", Alias = "cnt" }
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
                new() { Function = "sum", Field = "Orders.Total", Alias = "total" }
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
                new() { Function = "count", Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition
            {
                Function = "count",
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
                Function = "sum",
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
                new() { Function = "count", Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition
            {
                Function = "count",
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
                new() { Function = "count", Field = "Id", Alias = "cnt" }
            },
            Having = new HavingCondition { Function = "count", Field = "Id", Operator = "gt", Value = "0" }
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
                new() { Function = "count", Field = "Id", Alias = "cnt" },
                new() { Function = "sum", Field = "SSN", Alias = "total_ssn" }
            },
            Having = new HavingCondition { Function = "count", Field = "Id", Operator = "gt", Value = "0" }
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

        Action act = () => FieldAccessValidator.ValidateDefaultSortFieldConfiguration(execOptions);

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

        Action act = () => FieldAccessValidator.ValidateDefaultSortFieldConfiguration(execOptions);

        act.Should().Throw<QueryValidationException>()
           .Which.Message.Should().Contain("BlockedFields");
    }

    private class MockResolver : IFieldAccessResolver
    {
        private readonly bool _allowed;
        public MockResolver(bool allowed) => _allowed = allowed;
        public bool IsAllowed(string fieldPath, QueryOperation operation, QueryContext context) => _allowed;
    }
}
