using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class ValidationTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
    }

    [Fact]
    public void Should_Fail_When_Field_Does_Not_Exist()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "NonExistent:eq:101" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND");
    }

    [Fact]
    public void Should_Fail_When_Operator_Is_Invalid()
    {
        // Manually construct options with an invalid operator to bypass parser-level validation
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "Name", Operator = "invalid_op", Value = "john" }
                }
            }
        };
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "INVALID_OPERATOR");
    }

    [Fact]
    public void Should_Fail_When_Type_Is_Incompatible()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "id:eq:not_a_number" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "TYPE_MISMATCH");
    }

    [Fact]
    public void Should_Fail_When_Nested_Field_Does_Not_Exist()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "query", "orders.any(Unknown = 1)" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND");
    }

    [Fact]
    public void Should_Fail_When_Scoped_Filter_On_Non_Collection()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "query", "name.any(id = 1)" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "NOT_A_COLLECTION");
    }

    [Fact]
    public void Should_Succeed_When_Query_Is_Valid()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "name:eq:john" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => query.ApplyValidatedQueryOptions(options);

        act.Should().NotThrow();
    }
}
