using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Jql;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Security;
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

    private class MockResolver : IFieldAccessResolver
    {
        private readonly bool _allowed;
        public MockResolver(bool allowed) => _allowed = allowed;
        public bool IsAllowed(string fieldPath, QueryOperation operation, QueryContext context) => _allowed;
    }
}
