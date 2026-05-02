using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
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
        options.BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Field_Is_Not_Whitelisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }; // Only Id is allowed
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Nested_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "query", "orders.any(Status = 'Cancelled')" } });
        options.BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.Status" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED" && string.Equals(e.Field, "Orders.Status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_Fail_When_Sort_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "SSN:desc" } });
        options.BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Select_Field_Is_Blacklisted()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id,SSN" } });
        options.BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Succeed_When_All_Fields_Are_Allowed()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name", "Id" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Allow_Wildcards_In_Whitelist()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "query", "Orders.any(Status = 'Cancelled' AND Total > 0)" } });
        options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Orders", "Orders.*" };
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_Block_Field_Via_Custom_Resolver()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        options.FieldAccessResolver = new MockResolver(allowed: false);
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    [Fact]
    public void Should_Fail_When_Field_Depth_Is_Exceeded()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.Items.Id:eq:1" } });
        options.MaxFieldDepth = 2; // Orders.Items is depth 2, Orders.Items.Id is depth 3
        
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_ACCESS_DENIED");
    }

    private class MockResolver : IFieldAccessResolver
    {
        private readonly bool _allowed;
        public MockResolver(bool allowed) => _allowed = allowed;
        public bool IsAllowed(string fieldPath, QueryOperation operation, QueryContext context) => _allowed;
    }
}
